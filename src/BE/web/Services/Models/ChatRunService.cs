using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB.Extensions;
using Chats.BE.DB.Jsons;
using Chats.BE.Services.Common;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Options;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Chats.BE.Services.Models;

public class ChatRunService(
    IServiceScopeFactory serviceScopeFactory,
    ChatFactory chatFactory,
    BalanceService balanceService,
    IOptions<ChatOptions> chatOptions,
    ILogger<ChatRunService> logger)
{
    public async Task<ChatRunResult> RunAsync(
        ChatRunRequest request,
        Func<ChatRunSegmentContext, CancellationToken, Task> onSegment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onSegment);

        int userId = request.UserModel.UserId;

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        FileUrlProvider fileUrlProvider = scope.ServiceProvider.GetRequiredService<FileUrlProvider>();
        ClientInfoManager clientInfoManager = scope.ServiceProvider.GetRequiredService<ClientInfoManager>();

        int clientInfoId = await clientInfoManager.GetClientInfoId(cancellationToken);
        UserBalance userBalance = await db.UserBalances
            .Where(x => x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("User balance not found.");

        SingleModelBalanceCalculator balance = new(request.UserModel, userBalance.Balance);
        ChatService chatService = chatFactory.CreateChatService(request.UserModel.Model);
        ChatRunRuntime runtime = new(Stopwatch.GetTimestamp(), request.UserModel);

        Exception? exception = null;
        try
        {
            await runtime.ExecuteAsync(
                balance,
                chatService,
                request.ChatRequest,
                fileUrlProvider,
                chatOptions.Value.Retry429Times,
                logger,
                async (segment, ct) =>
                {
                    await onSegment(new ChatRunSegmentContext
                    {
                        Segment = segment,
                        ReasoningDurationMs = runtime.ReasoningDurationMs,
                    }, ct);
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
            runtime.RecordFailure(ex, request.ChatRequest);
        }

        UserModelUsage usage = runtime.CreateUserModelUsage(userId, balance, request.UserModel, clientInfoId, request.ChatRequest.Source);
        db.UserModelUsages.Add(usage);
        await db.SaveChangesAsync(CancellationToken.None);

        if (balance.Cost.TotalCost > 0)
        {
            await balanceService.UpdateBalance(db, userId, CancellationToken.None);
        }
        if (balance.HasUsageCost)
        {
            await balanceService.UpdateUsage(db, request.UserModel.Id, CancellationToken.None);
        }

        logger.LogDebug("Chat run finished with reason {FinishReason}, usageId={UsageId}", runtime.FinishReason, usage.Id);

        return new ChatRunResult
        {
            FullResponse = runtime.FullResponse ?? throw new InvalidOperationException("FullResponse is not available."),
            FinishReason = runtime.FinishReason,
            ReasoningDurationMs = runtime.ReasoningDurationMs,
            ElapsedTime = runtime.ElapsedTime,
            UserModelUsageId = usage.Id,
            Exception = exception,
        };
    }

    private sealed class ChatRunRuntime(long firstTick, UserModel userModel)
    {
        private long _preprocessTick;
        private long _firstReasoningTick;
        private long _firstResponseTick;
        private long _endResponseTick;
        private long _finishTick;
        private short _segmentCount;
        private bool _shouldFinalizeUsage;
        private readonly List<ChatSegment> _segments = [];
        private readonly List<ChatSegment> _segmentsAfterUsage = [];
        private UsageChatSegment? _lastReliableUsageSegment;
        private JsonPriceConfig? _priceConfig;

        public DBFinishReason FinishReason { get; private set; } = DBFinishReason.Success;

        public ChatCompletionSnapshot? FullResponse { get; private set; }

        public int ReasoningDurationMs => _segments.OfType<ThinkChatSegment>().Any()
            ? (int)Stopwatch.GetElapsedTime(_firstReasoningTick, _firstResponseTick).TotalMilliseconds
            : 0;

        public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(firstTick, Stopwatch.GetTimestamp());

        public async Task ExecuteAsync(
            SingleModelBalanceCalculator balance,
            ChatService chatService,
            ChatRequest request,
            FileUrlProvider fileUrlProvider,
            int? retry429Times,
            ILogger<ChatRunService> logger,
            Func<ChatSegment, CancellationToken, Task> onSegment,
            CancellationToken cancellationToken)
        {
            int attempt = 0;

            while (true)
            {
                bool yieldedAny = false;

                try
                {
                    await RunAttemptAsync(balance, chatService, request, fileUrlProvider, async (segment, ct) =>
                    {
                        yieldedAny = true;
                        await onSegment(segment, ct);
                    }, cancellationToken);
                    return;
                }
                catch (RawChatServiceException ex) when (
                    !yieldedAny &&
                    retry429Times is int maxRetries &&
                    maxRetries > 0 &&
                    attempt < maxRetries &&
                    ex.StatusCode == 429)
                {
                    attempt++;
                    logger.LogWarning(ex, "Retrying chat run after upstream 429. Attempt {Attempt}", attempt);
                    await Task.Delay(GetExponentialBackoffDelay(attempt), cancellationToken);
                }
            }
        }

        public void RecordFailure(Exception exception, ChatRequest request)
        {
            FinishReason = exception switch
            {
                ChatServiceException cse => cse.ErrorCode,
                AggregateException { InnerException: TaskCanceledException } => DBFinishReason.Cancelled,
                TaskCanceledException => DBFinishReason.Cancelled,
                _ => DBFinishReason.UnknownError,
            };

            if (FullResponse == null)
            {
                FullResponse = new ChatCompletionSnapshot
                {
                    Segments = [.. _segments],
                    Usage = _shouldFinalizeUsage ? EstimateUsageWithoutReliableSegment(request) : ChatTokenUsage.Zero,
                    IsUsageReliable = false,
                    FinishReason = FinishReason,
                };
            }

            if (_finishTick == _preprocessTick)
            {
                _finishTick = Stopwatch.GetTimestamp();
            }
        }

        public UserModelUsage CreateUserModelUsage(int userId, SingleModelBalanceCalculator balance, UserModel model, int clientInfoId, UsageSource source)
        {
            if (_finishTick == _preprocessTick)
            {
                _finishTick = Stopwatch.GetTimestamp();
            }

            ChatCompletionSnapshot snapshot = FullResponse ?? throw new InvalidOperationException("FullResponse is not available.");

            UserModelUsage usage = new()
            {
                ModelId = model.ModelId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                FinishReasonId = (byte)FinishReason,
                SegmentCount = _segmentCount,
                PreprocessDurationMs = (int)Stopwatch.GetElapsedTime(firstTick, _preprocessTick).TotalMilliseconds,
                ReasoningDurationMs = ReasoningDurationMs,
                FirstResponseDurationMs = (int)Stopwatch.GetElapsedTime(_preprocessTick, _firstReasoningTick != _preprocessTick ? _firstReasoningTick : _firstResponseTick).TotalMilliseconds,
                PostprocessDurationMs = (int)Stopwatch.GetElapsedTime(_endResponseTick, _finishTick).TotalMilliseconds,
                TotalDurationMs = (int)Stopwatch.GetElapsedTime(firstTick, _finishTick).TotalMilliseconds,
                InputFreshTokens = snapshot.Usage.InputFreshTokens,
                OutputTokens = snapshot.Usage.OutputTokens,
                ReasoningTokens = snapshot.Usage.ReasoningTokens,
                InputCachedTokens = snapshot.Usage.CacheTokens,
                IsUsageReliable = snapshot.IsUsageReliable,
                InputFreshCost = balance.Cost.InputFreshCost,
                OutputCost = balance.Cost.OutputCost,
                InputCachedCost = balance.Cost.InputCachedCost,
                ClientInfoId = clientInfoId,
                SourceId = (byte)source,
            };

            byte transactionTypeId = (byte)(source switch
            {
                UsageSource.Api => DBTransactionType.ApiCost,
                UsageSource.Summary => DBTransactionType.SummaryCost,
                _ => DBTransactionType.WebChatCost,
            });
            if (balance.Cost.TotalCost > 0)
            {
                usage.BalanceTransaction = new()
                {
                    UserId = userId,
                    CreatedAt = usage.CreatedAt,
                    CreditUserId = userId,
                    Amount = -balance.Cost.TotalCost,
                    TransactionTypeId = transactionTypeId,
                };
            }
            if (balance.HasUsageCost)
            {
                usage.UsageTransaction = new()
                {
                    ModelId = model.ModelId,
                    CreditUserId = userId,
                    CreatedAt = usage.CreatedAt,
                    CountAmount = -balance.Cost.Counts,
                    TokenAmount = -balance.Cost.Tokens,
                    TransactionTypeId = transactionTypeId,
                };
            }

            return usage;
        }

        private async Task RunAttemptAsync(
            SingleModelBalanceCalculator balance,
            ChatService chatService,
            ChatRequest request,
            FileUrlProvider fileUrlProvider,
            Func<ChatSegment, CancellationToken, Task> onSegment,
            CancellationToken cancellationToken)
        {
            ResetAttemptState();
            _priceConfig = userModel.Model.ToPriceConfig();

            try
            {
                if (userModel.ExpiresAt.IsExpired())
                {
                    throw new SubscriptionExpiredException(userModel.ExpiresAt);
                }
                if (!balance.IsSufficient)
                {
                    throw new InsufficientBalanceException();
                }

                balance.SetCost(1, 0, 0, _priceConfig);
                if (!balance.IsSufficient)
                {
                    throw new InsufficientBalanceException();
                }

                _shouldFinalizeUsage = true;

                await foreach (ChatSegment seg in chatService.ChatEntry(request, fileUrlProvider, cancellationToken).WithCancellation(cancellationToken))
                {
                    switch (seg)
                    {
                        case FinishReasonChatSegment stop:
                            FinishReason = stop.FinishReason ?? FinishReason;
                            break;
                        case UsageChatSegment usage:
                            RegisterUsage(usage, balance);
                            break;
                        default:
                            RegisterContentSegment(seg);
                            break;
                    }

                    await onSegment(seg, cancellationToken);
                }
            }
            finally
            {
                _endResponseTick = Stopwatch.GetTimestamp();
                FinalizeUsage(request, balance);
            }
        }

        private void ResetAttemptState()
        {
            _segments.Clear();
            _segmentsAfterUsage.Clear();
            _segmentCount = 0;
            _lastReliableUsageSegment = null;
            _shouldFinalizeUsage = false;
            FullResponse = null;
            FinishReason = DBFinishReason.Success;

            _preprocessTick = _firstReasoningTick = _firstResponseTick = _endResponseTick = _finishTick = Stopwatch.GetTimestamp();
        }

        private void FinalizeUsage(ChatRequest request, SingleModelBalanceCalculator balance)
        {
            if (!_shouldFinalizeUsage || _priceConfig == null)
            {
                FullResponse ??= new ChatCompletionSnapshot
                {
                    Segments = [.. _segments],
                    Usage = ChatTokenUsage.Zero,
                    IsUsageReliable = false,
                    FinishReason = FinishReason,
                };
                return;
            }

            (ChatTokenUsage finalUsage, bool isReliable) = EnsureFinalUsage(request);
            FullResponse = new ChatCompletionSnapshot
            {
                Segments = [.. _segments],
                Usage = finalUsage,
                IsUsageReliable = isReliable,
                FinishReason = FinishReason,
            };

            balance.SetCost(finalUsage.InputTokens, finalUsage.OutputTokens, finalUsage.CacheTokens, _priceConfig);
            if (!balance.IsSufficient)
            {
                FinishReason = DBFinishReason.InsufficientBalance;
                FullResponse = FullResponse with { FinishReason = FinishReason };
                throw new InsufficientBalanceException();
            }
        }

        private void RegisterUsage(UsageChatSegment usage, SingleModelBalanceCalculator balance)
        {
            if (_priceConfig == null)
            {
                throw new InvalidOperationException("Chat run has not been initialized.");
            }

            balance.SetCost(usage.Usage.InputTokens, usage.Usage.OutputTokens, usage.Usage.CacheTokens, _priceConfig);
            if (!balance.IsSufficient)
            {
                FinishReason = DBFinishReason.InsufficientBalance;
                throw new InsufficientBalanceException();
            }

            _lastReliableUsageSegment = usage;
            _segmentsAfterUsage.Clear();
        }

        private void RegisterContentSegment(ChatSegment segment)
        {
            if (segment is ThinkChatSegment && _firstReasoningTick == _preprocessTick)
            {
                _firstReasoningTick = Stopwatch.GetTimestamp();
            }
            if (segment is TextChatSegment or ImageChatSegment or ToolCallSegment && _firstResponseTick == _preprocessTick)
            {
                _firstResponseTick = Stopwatch.GetTimestamp();
            }

            if (segment is TextChatSegment or ImageChatSegment or ToolCallSegment)
            {
                _segmentCount++;
            }

            _segments.AddMerged(segment);
            _segmentsAfterUsage.AddMerged(segment);
        }

        private (ChatTokenUsage usage, bool isReliable) EnsureFinalUsage(ChatRequest chatRequest)
        {
            ChatTokenUsage baseUsage = _lastReliableUsageSegment?.Usage ?? ChatTokenUsage.Zero;
            ChatTokenUsage additionalUsage = CalculateUsageFromSegments(_segmentsAfterUsage);

            bool hasReliableBase = _lastReliableUsageSegment != null;
            bool hasAdditional = additionalUsage.OutputTokens > 0 || additionalUsage.ReasoningTokens > 0;

            if (hasReliableBase)
            {
                if (hasAdditional)
                {
                    return (new ChatTokenUsage
                    {
                        InputTokens = baseUsage.InputTokens,
                        OutputTokens = baseUsage.OutputTokens + additionalUsage.OutputTokens,
                        ReasoningTokens = baseUsage.ReasoningTokens + additionalUsage.ReasoningTokens,
                        CacheTokens = baseUsage.CacheTokens,
                        CacheCreationTokens = baseUsage.CacheCreationTokens,
                    }, false);
                }

                return (baseUsage, true);
            }

            return (EstimateUsageWithoutReliableSegment(chatRequest), false);
        }

        private ChatTokenUsage EstimateUsageWithoutReliableSegment(ChatRequest chatRequest)
        {
            ChatTokenUsage additionalUsage = CalculateUsageFromSegments(_segmentsAfterUsage);
            int promptTokens = chatRequest.EstimatePromptTokens(ChatService.Tokenizer);

            return additionalUsage with { InputTokens = promptTokens };
        }

        private static ChatTokenUsage CalculateUsageFromSegments(IEnumerable<ChatSegment> segments)
        {
            int textTokens = segments.OfType<TextChatSegment>().Sum(t => ChatService.Tokenizer.CountTokens(t.Text));
            int reasoningTokens = segments.OfType<ThinkChatSegment>().Sum(t => ChatService.Tokenizer.CountTokens(t.Think));

            return new ChatTokenUsage
            {
                InputTokens = 0,
                OutputTokens = textTokens + reasoningTokens,
                ReasoningTokens = reasoningTokens,
                CacheTokens = 0,
                CacheCreationTokens = 0,
            };
        }

        private static TimeSpan GetExponentialBackoffDelay(int attempt)
        {
            double seconds = Math.Min(30, Math.Pow(2, attempt - 1));
            int jitterMs = Random.Shared.Next(0, 250);
            return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(jitterMs);
        }
    }

    private sealed class SingleModelBalanceCalculator
    {
        private readonly int _initialCountBalance;
        private readonly int _initialTokenBalance;
        private readonly decimal _initialBalance;

        public SingleModelBalanceCalculator(UserModel userModel, decimal balance)
        {
            _initialCountBalance = userModel.CountBalance;
            _initialTokenBalance = userModel.TokenBalance;
            _initialBalance = balance;
        }

        public SingleModelBalanceCost Cost { get; private set; } = SingleModelBalanceCost.Zero;

        public bool IsSufficient => _initialCountBalance - Cost.Counts >= 0
            && _initialTokenBalance - Cost.Tokens >= 0
            && _initialBalance - Cost.TotalCost >= 0;

        public bool HasUsageCost => Cost.Counts > 0 || Cost.Tokens > 0;

        public void SetCost(int inputTokenCount, int outputTokenCount, int cacheTokenCount, JsonPriceConfig price)
        {
            if (_initialCountBalance > 0)
            {
                Cost = new SingleModelBalanceCost(Counts: 1, Tokens: 0, InputFreshCost: 0, OutputCost: 0, InputCachedCost: 0);
                return;
            }

            if (_initialTokenBalance > inputTokenCount + outputTokenCount)
            {
                Cost = new SingleModelBalanceCost(Counts: 0, Tokens: inputTokenCount + outputTokenCount, InputFreshCost: 0, OutputCost: 0, InputCachedCost: 0);
                return;
            }

            int remainingTokens = _initialTokenBalance;
            int toBeDeductedOutputTokens = Math.Max(0, outputTokenCount - remainingTokens);
            remainingTokens = Math.Max(0, remainingTokens - outputTokenCount);

            cacheTokenCount = Math.Clamp(cacheTokenCount, 0, inputTokenCount);
            int normalInputTokens = inputTokenCount - cacheTokenCount;
            int normalTokensCharged = Math.Max(0, normalInputTokens - remainingTokens);
            int allowanceAfterNormal = Math.Max(0, remainingTokens - normalInputTokens);
            int cacheTokensCharged = Math.Max(0, cacheTokenCount - allowanceAfterNormal);

            decimal inputCost = price.InputFreshTokenPrice * normalTokensCharged;
            decimal cacheCost = price.InputCachedTokenPrice * cacheTokensCharged;
            decimal outputCost = price.OutputTokenPrice * toBeDeductedOutputTokens;

            Cost = new SingleModelBalanceCost(
                Counts: 0,
                Tokens: _initialTokenBalance - remainingTokens,
                InputFreshCost: inputCost,
                OutputCost: outputCost,
                InputCachedCost: cacheCost);
        }
    }

    private readonly record struct SingleModelBalanceCost(int Counts, int Tokens, decimal InputFreshCost, decimal OutputCost, decimal InputCachedCost)
    {
        public static SingleModelBalanceCost Zero { get; } = new(0, 0, 0, 0, 0);

        public decimal TotalCost => InputFreshCost + OutputCost + InputCachedCost;
    }
}
