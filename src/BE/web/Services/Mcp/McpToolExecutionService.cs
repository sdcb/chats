using System.Diagnostics;
using System.Text.Json;
using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.RequestTracing;
using Chats.BE.Services.Models;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Chats.BE.Services.Mcp;

public sealed record McpToolExecutionRequest(
    string ServerName,
    string ServerUrl,
    IReadOnlyDictionary<string, string> Headers,
    string ToolName,
    string Parameters,
    bool Idempotent);

public sealed record McpToolAttemptResult(bool IsSuccess, string Result, bool Retryable);

public sealed record McpToolExecutionResult(bool IsSuccess, string Result, int DurationMs, int Attempts);

public interface IMcpToolAttemptExecutor
{
    Task<McpToolAttemptResult> ExecuteAsync(
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
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<McpToolAttemptExecutor> logger) : IMcpToolAttemptExecutor
{
    public async Task<McpToolAttemptResult> ExecuteAsync(
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

        try
        {
            await using HttpClientTransport transport = new(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(request.ServerUrl),
                    AdditionalHeaders = new Dictionary<string, string>(request.Headers),
                },
                httpClientFactory.CreateClient(HttpClientNames.ChatControllerMcp),
                loggerFactory,
                ownsHttpClient: false);
            await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            CallToolResult result = await client.CallToolAsync(
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
                cancellationToken: cancellationToken);

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
    ILogger<McpToolExecutionService> logger)
{
    public async Task<McpToolExecutionResult> ExecuteAsync(
        McpToolExecutionRequest request,
        Action<ToolProgressDelta> reportProgress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int maxAttempts = request.Idempotent ? 3 : 1;
        McpToolAttemptResult last = new(false, "Tool did not produce a result", false);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last = await attemptExecutor.ExecuteAsync(request, reportProgress, cancellationToken);
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
