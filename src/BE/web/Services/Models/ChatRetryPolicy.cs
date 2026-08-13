using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Options;
using Chats.DB.Enums;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.Models;

public sealed class ChatRetryPolicy
{
    internal const int DefaultMaxRetries = 5;
    internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private static readonly HashSet<int> RetryableStatusCodes = [408, 429, 500, 502, 503, 504];

    private readonly ILogger<ChatRetryPolicy> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<int, int> _nextJitter;

    public ChatRetryPolicy(IOptions<ChatOptions> options, ILogger<ChatRetryPolicy> logger)
        : this(options.Value, logger, Task.Delay, maxValue => Random.Shared.Next(0, maxValue))
    {
    }

    internal ChatRetryPolicy(
        ChatOptions options,
        ILogger<ChatRetryPolicy> logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<int, int> nextJitter)
    {
        _logger = logger;
        _delayAsync = delayAsync;
        _nextJitter = nextJitter;

        if (options.MaxTransientRetries is int maxTransientRetries)
        {
            MaxRetries = Math.Max(0, maxTransientRetries);
            return;
        }

        MaxRetries = DefaultMaxRetries;
    }

    internal int MaxRetries { get; }

    internal async Task ExecuteAsync(
        DBApiType apiType,
        Func<Action, CancellationToken, Task> runAttemptAsync,
        CancellationToken cancellationToken)
    {
        int retriesCompleted = 0;

        while (true)
        {
            bool yieldedAny = false;

            try
            {
                await runAttemptAsync(() => yieldedAny = true, cancellationToken);
                return;
            }
            catch (Exception ex) when (ShouldRetry(ex, yieldedAny, apiType, cancellationToken, retriesCompleted))
            {
                retriesCompleted++;
                TimeSpan delay = GetDelay(ex, retriesCompleted);
                int? statusCode = (ex as RawChatServiceException)?.StatusCode;

                _logger.LogWarning(
                    ex,
                    "Retrying text chat after transient upstream failure. StatusCode={StatusCode}, ExceptionType={ExceptionType}, Retry={Retry}/{MaxRetries}, DelayMs={DelayMs}",
                    statusCode,
                    ex.GetType().Name,
                    retriesCompleted,
                    MaxRetries,
                    delay.TotalMilliseconds);

                await _delayAsync(delay, cancellationToken);
            }
        }
    }

    internal bool ShouldRetry(
        Exception exception,
        bool yieldedAny,
        DBApiType apiType,
        CancellationToken cancellationToken,
        int retriesCompleted)
    {
        if (yieldedAny || apiType == DBApiType.OpenAIImageGeneration || retriesCompleted >= MaxRetries)
        {
            return false;
        }

        return exception switch
        {
            RawChatServiceException raw => RetryableStatusCodes.Contains(raw.StatusCode),
            HttpRequestException => true,
            IOException => true,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false,
        };
    }

    internal TimeSpan GetDelay(Exception exception, int retryNumber)
    {
        if (exception is RawChatServiceException { RetryAfter: TimeSpan retryAfter })
        {
            return ClampDelay(retryAfter);
        }

        double seconds = Math.Pow(2, Math.Max(0, retryNumber - 1));
        TimeSpan delay = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(_nextJitter(250));
        return ClampDelay(delay);
    }

    private static TimeSpan ClampDelay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay <= MaxDelay ? delay : MaxDelay;
    }
}
