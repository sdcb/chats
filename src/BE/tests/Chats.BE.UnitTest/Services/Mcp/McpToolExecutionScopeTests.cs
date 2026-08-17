using Chats.BE.Services.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolExecutionScopeTests
{
    [Fact]
    public async Task ScopeWithoutCalls_DoesNotCreateClient()
    {
        FakeClientFactory factory = new();
        McpToolExecutionService service = CreateService(factory);

        await using (service.CreateScope()) { }

        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task SameServer_ConcurrentCalls_CreateAndDisposeOneClient()
    {
        TaskCompletionSource allowCreation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeClient client = new(_ => SuccessfulResult("ok"));
        FakeClientFactory factory = new(async _ =>
        {
            await allowCreation.Task;
            return client;
        });
        McpToolExecutionService service = CreateService(factory);

        await using (McpToolExecutionScope scope = service.CreateScope())
        {
            Task<McpToolExecutionResult> first = ExecuteAsync(service, scope, Request(serverId: 1));
            Task<McpToolExecutionResult> second = ExecuteAsync(service, scope, Request(serverId: 1));
            await WaitUntilAsync(() => factory.CreateCount == 1);
            allowCreation.SetResult();

            McpToolExecutionResult[] results = await Task.WhenAll(first, second);

            Assert.All(results, result => Assert.True(result.IsSuccess));
            Assert.Equal(1, factory.CreateCount);
            Assert.Equal(2, client.CallCount);
            Assert.Equal(0, client.DisposeCount);
        }

        Assert.Equal(1, client.DisposeCount);
    }

    [Fact]
    public async Task DifferentServers_CreateSeparateClients()
    {
        FakeClientFactory factory = new(
            _ => Task.FromResult<IMcpToolClient>(new FakeClient(_ => SuccessfulResult("one"))),
            _ => Task.FromResult<IMcpToolClient>(new FakeClient(_ => SuccessfulResult("two"))));
        McpToolExecutionService service = CreateService(factory);

        await using McpToolExecutionScope scope = service.CreateScope();
        await Task.WhenAll(
            ExecuteAsync(service, scope, Request(serverId: 1)),
            ExecuteAsync(service, scope, Request(serverId: 2)));

        Assert.Equal(2, factory.CreateCount);
    }

    [Fact]
    public async Task ToolErrorRetry_ReusesHealthyClient()
    {
        Queue<CallToolResult> results = new([
            new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "retry" }] },
            SuccessfulResult("ok"),
        ]);
        FakeClient client = new(_ => results.Dequeue());
        FakeClientFactory factory = new(_ => Task.FromResult<IMcpToolClient>(client));
        McpToolExecutionService service = CreateService(factory);

        await using McpToolExecutionScope scope = service.CreateScope();
        McpToolExecutionResult result = await ExecuteAsync(service, scope, Request(serverId: 1, idempotent: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task TransportFailureRetry_ReplacesClientGeneration()
    {
        FakeClient failedClient = new((Func<int, CallToolResult>)(_ =>
            throw new HttpRequestException("connection closed")));
        FakeClient replacementClient = new(_ => SuccessfulResult("ok"));
        FakeClientFactory factory = new(
            _ => Task.FromResult<IMcpToolClient>(failedClient),
            _ => Task.FromResult<IMcpToolClient>(replacementClient));
        McpToolExecutionService service = CreateService(factory);

        await using (McpToolExecutionScope scope = service.CreateScope())
        {
            McpToolExecutionResult result = await ExecuteAsync(
                service,
                scope,
                Request(serverId: 1, idempotent: true));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Attempts);
            Assert.Equal(2, factory.CreateCount);
            Assert.Equal(0, failedClient.DisposeCount);
            Assert.Equal(0, replacementClient.DisposeCount);
        }

        Assert.Equal(1, failedClient.DisposeCount);
        Assert.Equal(1, replacementClient.DisposeCount);
    }

    [Fact]
    public async Task InitializationFailureRetry_CreatesNewGeneration()
    {
        FakeClient replacementClient = new(_ => SuccessfulResult("ok"));
        FakeClientFactory factory = new(
            _ => Task.FromException<IMcpToolClient>(new HttpRequestException("initialize failed")),
            _ => Task.FromResult<IMcpToolClient>(replacementClient));
        McpToolExecutionService service = CreateService(factory);

        await using McpToolExecutionScope scope = service.CreateScope();
        McpToolExecutionResult result = await ExecuteAsync(
            service,
            scope,
            Request(serverId: 1, idempotent: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(1, replacementClient.CallCount);
    }

    [Fact]
    public async Task InvalidParameters_DoNotCreateClient()
    {
        FakeClientFactory factory = new();
        McpToolExecutionService service = CreateService(factory);
        McpToolExecutionRequest request = Request(serverId: 1) with { Parameters = "{" };

        await using McpToolExecutionScope scope = service.CreateScope();
        McpToolExecutionResult result = await ExecuteAsync(service, scope, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task NonIdempotentTransportFailure_InvalidatesClientForLaterCall()
    {
        FakeClient failedClient = new((Func<int, CallToolResult>)(_ =>
            throw new HttpRequestException("connection closed")));
        FakeClient replacementClient = new(_ => SuccessfulResult("ok"));
        FakeClientFactory factory = new(
            _ => Task.FromResult<IMcpToolClient>(failedClient),
            _ => Task.FromResult<IMcpToolClient>(replacementClient));
        McpToolExecutionService service = CreateService(factory);

        await using McpToolExecutionScope scope = service.CreateScope();
        McpToolExecutionResult failed = await ExecuteAsync(service, scope, Request(serverId: 1));
        McpToolExecutionResult succeeded = await ExecuteAsync(service, scope, Request(serverId: 1));

        Assert.False(failed.IsSuccess);
        Assert.Equal(1, failed.Attempts);
        Assert.True(succeeded.IsSuccess);
        Assert.Equal(2, factory.CreateCount);
    }

    [Fact]
    public async Task ConcurrentTransportFailures_ShareOneReplacementGeneration()
    {
        int failedCalls = 0;
        TaskCompletionSource bothCallsEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeClient failedClient = new(async _ =>
        {
            if (Interlocked.Increment(ref failedCalls) == 2)
            {
                bothCallsEntered.SetResult();
            }
            await bothCallsEntered.Task;
            throw new HttpRequestException("session closed");
        });
        FakeClient replacementClient = new(_ => SuccessfulResult("ok"));
        FakeClientFactory factory = new(
            _ => Task.FromResult<IMcpToolClient>(failedClient),
            _ => Task.FromResult<IMcpToolClient>(replacementClient));
        McpToolExecutionService service = CreateService(factory);

        await using McpToolExecutionScope scope = service.CreateScope();
        McpToolExecutionResult[] results = await Task.WhenAll(
            ExecuteAsync(service, scope, Request(serverId: 1, idempotent: true)),
            ExecuteAsync(service, scope, Request(serverId: 1, idempotent: true)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(2, failedClient.CallCount);
        Assert.Equal(2, replacementClient.CallCount);
    }

    [Fact]
    public async Task SeparateScopes_DoNotShareClients()
    {
        FakeClientFactory factory = new(
            _ => Task.FromResult<IMcpToolClient>(new FakeClient(_ => SuccessfulResult("one"))),
            _ => Task.FromResult<IMcpToolClient>(new FakeClient(_ => SuccessfulResult("two"))));
        McpToolExecutionService service = CreateService(factory);

        await using (McpToolExecutionScope firstScope = service.CreateScope())
        {
            await ExecuteAsync(service, firstScope, Request(serverId: 1));
        }
        await using (McpToolExecutionScope secondScope = service.CreateScope())
        {
            await ExecuteAsync(service, secondScope, Request(serverId: 1));
        }

        Assert.Equal(2, factory.CreateCount);
    }

    private static McpToolExecutionService CreateService(IMcpToolClientFactory factory)
        => new(
            new McpToolAttemptExecutor(NullLogger<McpToolAttemptExecutor>.Instance),
            new ImmediateRetryDelay(),
            factory,
            NullLogger<McpToolExecutionService>.Instance);

    private static Task<McpToolExecutionResult> ExecuteAsync(
        McpToolExecutionService service,
        McpToolExecutionScope scope,
        McpToolExecutionRequest request)
        => service.ExecuteAsync(scope, request, _ => { }, CancellationToken.None);

    private static McpToolExecutionRequest Request(int serverId, bool idempotent = false)
        => new(
            serverId,
            $"server-{serverId}",
            $"https://example.com/{serverId}/mcp",
            new Dictionary<string, string>(),
            "tool",
            "{}",
            idempotent);

    private static CallToolResult SuccessfulResult(string text)
        => new() { Content = [new TextContentBlock { Text = text }] };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ImmediateRetryDelay : IMcpRetryDelay
    {
        public Task DelayAsync(int completedAttempt, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeClientFactory(params Func<McpToolExecutionRequest, Task<IMcpToolClient>>[] factories)
        : IMcpToolClientFactory
    {
        private readonly Queue<Func<McpToolExecutionRequest, Task<IMcpToolClient>>> factories = new(factories);
        private int createCount;

        public int CreateCount => Volatile.Read(ref createCount);

        public Task<IMcpToolClient> CreateAsync(
            McpToolExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref createCount);
            lock (factories)
            {
                return factories.Count == 0
                    ? Task.FromResult<IMcpToolClient>(new FakeClient(_ => SuccessfulResult("ok")))
                    : factories.Dequeue()(request);
            }
        }
    }

    private sealed class FakeClient : IMcpToolClient
    {
        private readonly Func<int, Task<CallToolResult>> call;
        private int callCount;
        private int disposeCount;

        public FakeClient(Func<int, CallToolResult> call)
            : this(index => Task.FromResult(call(index)))
        {
        }

        public FakeClient(Func<int, Task<CallToolResult>> call)
        {
            this.call = call;
        }

        public int CallCount => Volatile.Read(ref callCount);
        public int DisposeCount => Volatile.Read(ref disposeCount);

        public async ValueTask<CallToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments,
            IProgress<ProgressNotificationValue>? progress,
            CancellationToken cancellationToken)
            => await call(Interlocked.Increment(ref callCount));

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposeCount);
            return ValueTask.CompletedTask;
        }
    }
}
