using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Services.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_NonIdempotentFailure_DoesNotRetry()
    {
        FakeAttemptExecutor executor = new(new McpToolAttemptResult(false, "failed", true));
        FakeRetryDelay delay = new();
        McpToolExecutionResult result = await CreateService(executor, delay)
            .ExecuteAsync(Request(idempotent: false), _ => { }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("failed", result.Result);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, executor.CallCount);
        Assert.Empty(delay.CompletedAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotentRetryableFailures_RetriesUntilSuccess()
    {
        FakeAttemptExecutor executor = new(
            new(false, "transport failed", true),
            new(false, "tool error", true),
            new(true, "ok", false));
        FakeRetryDelay delay = new();

        McpToolExecutionResult result = await CreateService(executor, delay)
            .ExecuteAsync(Request(idempotent: true), _ => { }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Result);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, executor.CallCount);
        Assert.Equal([1, 2], delay.CompletedAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotentFailure_StopsAfterThreeAttempts()
    {
        FakeAttemptExecutor executor = new(
            new(false, "first", true),
            new(false, "second", true),
            new(false, "last", true));
        FakeRetryDelay delay = new();

        McpToolExecutionResult result = await CreateService(executor, delay)
            .ExecuteAsync(Request(idempotent: true), _ => { }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("last", result.Result);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, executor.CallCount);
        Assert.Equal([1, 2], delay.CompletedAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotentDeterministicFailure_DoesNotRetry()
    {
        FakeAttemptExecutor executor = new(new McpToolAttemptResult(false, "invalid arguments", false));
        FakeRetryDelay delay = new();

        McpToolExecutionResult result = await CreateService(executor, delay)
            .ExecuteAsync(Request(idempotent: true), _ => { }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, executor.CallCount);
        Assert.Empty(delay.CompletedAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsProgressFromEveryAttempt()
    {
        FakeAttemptExecutor executor = new(
            new(false, "retry", true),
            new(true, "ok", false))
        {
            ProgressFactory = call => new StdOutToolProgressDelta { StdOutput = $"attempt-{call}" },
        };
        List<string?> progress = [];

        await CreateService(executor, new FakeRetryDelay()).ExecuteAsync(
            Request(idempotent: true),
            delta => progress.Add(Assert.IsType<StdOutToolProgressDelta>(delta).StdOutput),
            CancellationToken.None);

        Assert.Equal(["attempt-1", "attempt-2"], progress);
    }

    private static McpToolExecutionService CreateService(
        IMcpToolAttemptExecutor executor,
        IMcpRetryDelay delay)
        => new(executor, delay, NullLogger<McpToolExecutionService>.Instance);

    private static McpToolExecutionRequest Request(bool idempotent)
        => new(
            "server",
            "https://example.com/mcp",
            new Dictionary<string, string>(),
            "tool",
            "{}",
            idempotent);

    private sealed class FakeAttemptExecutor(params McpToolAttemptResult[] results) : IMcpToolAttemptExecutor
    {
        private readonly Queue<McpToolAttemptResult> results = new(results);

        public int CallCount { get; private set; }

        public Func<int, ToolProgressDelta?>? ProgressFactory { get; init; }

        public Task<McpToolAttemptResult> ExecuteAsync(
            McpToolExecutionRequest request,
            Action<ToolProgressDelta> reportProgress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ToolProgressDelta? progress = ProgressFactory?.Invoke(CallCount);
            if (progress is not null)
            {
                reportProgress(progress);
            }

            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class FakeRetryDelay : IMcpRetryDelay
    {
        public List<int> CompletedAttempts { get; } = [];

        public Task DelayAsync(int completedAttempt, CancellationToken cancellationToken)
        {
            CompletedAttempts.Add(completedAttempt);
            return Task.CompletedTask;
        }
    }
}
