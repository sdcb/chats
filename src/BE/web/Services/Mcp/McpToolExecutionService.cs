using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models;
using Chats.BE.Services.RequestTracing;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Chats.BE.Services.Mcp;

public sealed record McpToolExecutionRequest(
    int ServerId,
    string ServerName,
    string ServerUrl,
    IReadOnlyDictionary<string, string> Headers,
    string ToolName,
    string Parameters,
    bool Idempotent);

public sealed record McpToolAttemptResult(bool IsSuccess, string Result, bool Retryable);

public sealed record McpToolExecutionResult(bool IsSuccess, string Result, int DurationMs, int Attempts);

public interface IMcpToolClient : IAsyncDisposable
{
    ValueTask<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken);
}

public interface IMcpToolClientFactory
{
    Task<IMcpToolClient> CreateAsync(McpToolExecutionRequest request, CancellationToken cancellationToken);
}

public sealed class McpToolClientFactory(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IMcpToolClientFactory
{
    public async Task<IMcpToolClient> CreateAsync(
        McpToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        HttpClientTransport transport = new(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(request.ServerUrl),
                AdditionalHeaders = new Dictionary<string, string>(request.Headers),
            },
            httpClientFactory.CreateClient(HttpClientNames.ChatControllerMcp),
            loggerFactory,
            ownsHttpClient: false);

        try
        {
            McpClient client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            return new McpToolClient(client, transport);
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }

    private sealed class McpToolClient(McpClient client, HttpClientTransport transport) : IMcpToolClient
    {
        public ValueTask<CallToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments,
            IProgress<ProgressNotificationValue>? progress,
            CancellationToken cancellationToken)
            => client.CallToolAsync(toolName, arguments, progress, cancellationToken: cancellationToken);

        public async ValueTask DisposeAsync()
        {
            try
            {
                await client.DisposeAsync();
            }
            finally
            {
                await transport.DisposeAsync();
            }
        }
    }
}

public sealed class McpToolExecutionScope(
    IMcpToolClientFactory clientFactory,
    ILogger logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, ClientEntry> clients = [];
    private readonly ConcurrentBag<IMcpToolClient> createdClients = [];
    private int disposed;

    internal async Task<ClientLease> GetClientAsync(
        McpToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        ClientEntry entry = clients.GetOrAdd(
            request.ServerId,
            _ => new ClientEntry(new Lazy<Task<IMcpToolClient>>(
                () => CreateClientAsync(request, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication)));

        try
        {
            return new ClientLease(entry, await entry.Client.Value);
        }
        catch
        {
            Invalidate(request.ServerId, entry);
            throw;
        }
    }

    internal void Invalidate(int serverId, ClientEntry entry)
    {
        ((ICollection<KeyValuePair<int, ClientEntry>>)clients)
            .Remove(new KeyValuePair<int, ClientEntry>(serverId, entry));
    }

    private async Task<IMcpToolClient> CreateClientAsync(
        McpToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        IMcpToolClient client = await clientFactory.CreateAsync(request, cancellationToken);
        createdClients.Add(client);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        while (createdClients.TryTake(out IMcpToolClient? client))
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispose MCP client at the end of a tool-call step");
            }
        }

        clients.Clear();
    }

    internal sealed record ClientEntry(Lazy<Task<IMcpToolClient>> Client);
    internal sealed record ClientLease(ClientEntry Entry, IMcpToolClient Client);
}

public interface IMcpToolAttemptExecutor
{
    Task<McpToolAttemptResult> ExecuteAsync(
        McpToolExecutionScope scope,
        McpToolExecutionRequest request,
        Action<ToolProgressDelta> reportProgress,
        CancellationToken cancellationToken);
}

public interface IMcpRetryDelay
{
    Task DelayAsync(int completedAttempt, CancellationToken cancellationToken);
}

public sealed class McpRetryDelay : IMcpRetryDelay
{
    public Task DelayAsync(int completedAttempt, CancellationToken cancellationToken)
    {
        TimeSpan delay = TimeSpan.FromMilliseconds(
            (completedAttempt == 1 ? 1000 : 2000) + Random.Shared.Next(50, 251));
        return Task.Delay(delay, cancellationToken);
    }
}

public sealed class McpToolAttemptExecutor(
    ILogger<McpToolAttemptExecutor> logger) : IMcpToolAttemptExecutor
{
    public async Task<McpToolAttemptResult> ExecuteAsync(
        McpToolExecutionScope scope,
        McpToolExecutionRequest request,
        Action<ToolProgressDelta> reportProgress,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?>? arguments;
        try
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(request.Parameters);
        }
        catch (JsonException ex)
        {
            return new(false, ex.Message, false);
        }

        McpToolExecutionScope.ClientLease? lease = null;
        try
        {
            lease = await scope.GetClientAsync(request, cancellationToken);
            CallToolResult result = await lease.Client.CallToolAsync(
                request.ToolName,
                arguments,
                new ProgressReporter(progress =>
                {
                    if (string.IsNullOrWhiteSpace(progress.Message)) return;
                    try
                    {
                        ToolProgressDelta? delta = JsonSerializer.Deserialize<ToolProgressDelta>(progress.Message);
                        if (delta is not null and not ToolCompletedToolProgressDelta)
                        {
                            reportProgress(delta);
                        }
                    }
                    catch (JsonException)
                    {
                        // MCP progress is optional and may use a server-specific payload.
                    }
                }),
                cancellationToken);

            bool success = result.IsError is not true;
            string text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
            return new(success, text, Retryable: !success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsRetryableTransportFailure(ex, cancellationToken))
        {
            if (lease is not null)
            {
                scope.Invalidate(request.ServerId, lease.Entry);
            }
            logger.LogWarning(ex, "Retryable MCP transport failure calling {ServerName}/{ToolName}", request.ServerName, request.ToolName);
            return new(false, ex.Message, true);
        }
        catch (McpException ex)
        {
            return new(false, ex.Message, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP call failed for {ServerName}/{ToolName}", request.ServerName, request.ToolName);
            return new(false, ex.Message, false);
        }
    }

    private static bool IsRetryableTransportFailure(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is HttpRequestException or IOException or TimeoutException) return true;
        if (ex is OperationCanceledException) return !cancellationToken.IsCancellationRequested;
        if (ex is not McpException) return false;

        string message = ex.Message;
        return message.Contains("transport", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || message.Contains("session", StringComparison.OrdinalIgnoreCase)
            || message.Contains("closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("disconnect", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class McpToolExecutionService(
    IMcpToolAttemptExecutor attemptExecutor,
    IMcpRetryDelay retryDelay,
    IMcpToolClientFactory clientFactory,
    ILogger<McpToolExecutionService> logger)
{
    public McpToolExecutionScope CreateScope() => new(clientFactory, logger);

    public async Task<McpToolExecutionResult> ExecuteAsync(
        McpToolExecutionScope scope,
        McpToolExecutionRequest request,
        Action<ToolProgressDelta> reportProgress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int maxAttempts = request.Idempotent ? 3 : 1;
        McpToolAttemptResult last = new(false, "Tool did not produce a result", false);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last = await attemptExecutor.ExecuteAsync(scope, request, reportProgress, cancellationToken);
            if (last.IsSuccess || !last.Retryable || attempt == maxAttempts)
            {
                return new(last.IsSuccess, last.Result, (int)stopwatch.ElapsedMilliseconds, attempt);
            }

            logger.LogInformation(
                "Retrying idempotent MCP tool {ServerName}/{ToolName}; attempt {NextAttempt}/{MaxAttempts}",
                request.ServerName, request.ToolName, attempt + 1, maxAttempts);
            await retryDelay.DelayAsync(attempt, cancellationToken);
        }

        return new(last.IsSuccess, last.Result, (int)stopwatch.ElapsedMilliseconds, maxAttempts);
    }
}
