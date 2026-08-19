using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models;
using Chats.BE.Services.Options;
using Chats.DB.Enums;
using Microsoft.Extensions.Logging;

namespace Chats.BE.UnitTest.Services.Models;

public class ChatRetryPolicyTests
{
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void ShouldRetry_RetryableStatusBeforeFirstYield_ReturnsTrue(int statusCode)
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 });

        bool result = policy.ShouldRetry(
            new RawChatServiceException(statusCode, "error"),
            yieldedAny: false,
            DBApiType.OpenAIChatCompletion,
            CancellationToken.None,
            retriesCompleted: 0);

        Assert.True(result);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public void ShouldRetry_NonRetryableStatus_ReturnsFalse(int statusCode)
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 });

        bool result = policy.ShouldRetry(
            new RawChatServiceException(statusCode, "error"),
            yieldedAny: false,
            DBApiType.OpenAIChatCompletion,
            CancellationToken.None,
            retriesCompleted: 0);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetry_TransportFailuresBeforeFirstYield_ReturnsTrue()
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 });

        Assert.True(policy.ShouldRetry(new HttpRequestException(), false, DBApiType.OpenAIChatCompletion, CancellationToken.None, 0));
        Assert.True(policy.ShouldRetry(new IOException(), false, DBApiType.OpenAIResponse, CancellationToken.None, 0));
        Assert.True(policy.ShouldRetry(new OperationCanceledException(), false, DBApiType.AnthropicMessages, CancellationToken.None, 0));
    }

    [Fact]
    public void ShouldRetry_UserCancellation_ReturnsFalse()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 });

        bool result = policy.ShouldRetry(
            new OperationCanceledException(cancellationTokenSource.Token),
            yieldedAny: false,
            DBApiType.OpenAIChatCompletion,
            cancellationTokenSource.Token,
            retriesCompleted: 0);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetry_AfterYieldAtRetryLimitOrForImageGeneration_ReturnsFalse()
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 2 });
        RawChatServiceException exception = new(500, "error");

        Assert.False(policy.ShouldRetry(exception, true, DBApiType.OpenAIChatCompletion, CancellationToken.None, 0));
        Assert.False(policy.ShouldRetry(exception, false, DBApiType.OpenAIChatCompletion, CancellationToken.None, 2));
        Assert.False(policy.ShouldRetry(exception, false, DBApiType.OpenAIImageGeneration, CancellationToken.None, 0));
    }

    [Fact]
    public async Task ExecuteAsync_FirstAttemptReturns500_SecondAttemptSucceedsWithoutDuplicateOutput()
    {
        List<TimeSpan> delays = [];
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 }, delays);
        List<string> output = [];
        int attempts = 0;

        await policy.ExecuteAsync(DBApiType.OpenAIChatCompletion, (markYielded, _) =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new RawChatServiceException(500, "upstream error");
            }

            markYielded();
            output.Add("success");
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Single(delays);
        Assert.Equal(["success"], output);
    }

    [Fact]
    public async Task ExecuteAsync_AfterFirstYield_DoesNotRetry()
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 });
        int attempts = 0;

        await Assert.ThrowsAsync<RawChatServiceException>(() => policy.ExecuteAsync(
            DBApiType.OpenAIChatCompletion,
            (markYielded, _) =>
            {
                attempts++;
                markYielded();
                throw new RawChatServiceException(500, "upstream error");
            },
            CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_StopsAfterConfiguredRetries()
    {
        List<TimeSpan> delays = [];
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions { MaxTransientRetries = 5 }, delays);
        int attempts = 0;

        await Assert.ThrowsAsync<RawChatServiceException>(() => policy.ExecuteAsync(
            DBApiType.OpenAIChatCompletion,
            (_, _) =>
            {
                attempts++;
                throw new RawChatServiceException(500, "upstream error");
            },
            CancellationToken.None));

        Assert.Equal(6, attempts);
        Assert.Equal(5, delays.Count);
    }

    [Fact]
    public void Constructor_UsesDefaultAndClampsNegativeValues()
    {
        ChatRetryPolicy defaultPolicy = CreatePolicy(new ChatOptions());
        ChatRetryPolicy negativePolicy = CreatePolicy(new ChatOptions { MaxTransientRetries = -1 });

        Assert.Equal(ChatRetryPolicy.DefaultMaxRetries, defaultPolicy.MaxRetries);
        Assert.Equal(0, negativePolicy.MaxRetries);
    }

    [Fact]
    public void GetDelay_UsesExponentialBackoffAndJitter()
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions(), jitterMs: 249);

        Assert.Equal(TimeSpan.FromMilliseconds(1249), policy.GetDelay(new HttpRequestException(), 1));
        Assert.Equal(TimeSpan.FromMilliseconds(2249), policy.GetDelay(new HttpRequestException(), 2));
        Assert.Equal(TimeSpan.FromMilliseconds(16249), policy.GetDelay(new HttpRequestException(), 5));
    }

    [Fact]
    public void GetDelay_RetryAfterTakesPriorityAndIsClamped()
    {
        ChatRetryPolicy policy = CreatePolicy(new ChatOptions(), jitterMs: 249);

        Assert.Equal(TimeSpan.FromSeconds(12), policy.GetDelay(new RawChatServiceException(429, "error", TimeSpan.FromSeconds(12)), 1));
        Assert.Equal(ChatRetryPolicy.MaxDelay, policy.GetDelay(new RawChatServiceException(503, "error", TimeSpan.FromMinutes(2)), 1));
        Assert.Equal(TimeSpan.Zero, policy.GetDelay(new RawChatServiceException(503, "error", TimeSpan.FromSeconds(-1)), 1));
    }

    [Fact]
    public async Task RawChatServiceException_CreateAsync_ReadsRetryAfter()
    {
        using HttpResponseMessage response = new(System.Net.HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("upstream error"),
        };
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        RawChatServiceException exception = await RawChatServiceException.CreateAsync(response, CancellationToken.None);

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("upstream error", exception.Body);
        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
    }

    private static ChatRetryPolicy CreatePolicy(
        ChatOptions options,
        List<TimeSpan>? delays = null,
        RecordingLogger<ChatRetryPolicy>? logger = null,
        int jitterMs = 0)
    {
        RecordingLogger<ChatRetryPolicy> effectiveLogger = logger ?? new RecordingLogger<ChatRetryPolicy>();
        return new ChatRetryPolicy(
            options,
            effectiveLogger,
            (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            },
            _ => jitterMs);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public int WarningCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningCount++;
            }
        }
    }
}
