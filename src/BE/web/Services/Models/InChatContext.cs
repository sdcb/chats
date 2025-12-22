using Chats.Web.Controllers.Chats.Chats;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.DB.Jsons;
using Chats.Web.Services.Common;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.Models.ChatServices;
using Chats.Web.Services.Models.Dtos;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Chats.Web.Services.Models;

public class InChatContext(long firstTick)
{
    private long _preprocessTick, _firstReasoningTick, _firstResponseTick, _endResponseTick, _finishTick;
    private short _segmentCount;
    private readonly List<ChatSegment> _segments = [];
    private readonly List<ChatSegment> _segmentsAfterUsage = [];
    private UsageChatSegment? _lastReliableUsageSegment;
    private JsonPriceConfig? _priceConfig;
    private ScopedBalanceCalculator? _balance;

    public DBFinishReason FinishReason { get; set; } = DBFinishReason.Success;

    internal async IAsyncEnumerable<ChatSegment> RunInternal(
        ScopedBalanceCalculator balance,
        UserModel userModel,
        ChatRequest requestForEstimation,
        IAsyncEnumerable<ChatSegment> segments)
    {
        _balance = balance;

        _preprocessTick = _firstReasoningTick = _firstResponseTick = _endResponseTick = _finishTick = Stopwatch.GetTimestamp();
        if (userModel.ExpiresAt.IsExpired())
        {
            throw new SubscriptionExpiredException(userModel.ExpiresAt);
        }
        _priceConfig = userModel.Model.ToPriceConfig();
        if (!balance.IsSufficient)
        {
            throw new InsufficientBalanceException();
        }

        balance.SetCost(userModel.ModelId, 1, 0, 0, _priceConfig);
        if (!balance.IsSufficient)
        {
            throw new InsufficientBalanceException();
        }

        try
        {
            await foreach (ChatSegment seg in segments)
            {
                switch (seg)
                {
                    case FinishReasonChatSegment stop:
                        FinishReason = stop.FinishReason ?? FinishReason;
                        break;
                    case UsageChatSegment usage:
                        RegisterUsage(usage, userModel);
                        break;
                    default:
                        RegisterContentSegment(seg);
                        break;
                }

                yield return seg;
            }
        }
        finally
        {
            _endResponseTick = Stopwatch.GetTimestamp();
            (ChatTokenUsage finalUsage, bool isReliable) = EnsureFinalUsage(requestForEstimation, userModel);

            FullResponse = new ChatCompletionSnapshot
            {
                Segments = _segments,
                Usage = finalUsage,
                IsUsageReliable = isReliable,
                FinishReason = FinishReason
            };

            _balance.SetCost(userModel.ModelId, finalUsage.InputTokens, finalUsage.OutputTokens, finalUsage.CacheTokens, _priceConfig);
            if (!_balance.IsSufficient)
            {
                FinishReason = DBFinishReason.InsufficientBalance;
                throw new InsufficientBalanceException();
            }
        }
    }

    public async IAsyncEnumerable<ChatSegment> Run(
        ScopedBalanceCalculator balance,
        UserModel userModel,
        ChatService chatService,
        ChatRequest request,
        FileUrlProvider fup,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ChatSegment seg in RunInternal(balance, userModel, request, chatService.ChatEntry(request, fup, cancellationToken)))
        {
            yield return seg;
        }
    }

    private void RegisterUsage(UsageChatSegment usage, UserModel userModel)
    {
        if (_priceConfig == null || _balance == null)
        {
            throw new InvalidOperationException("InChatContext has not been initialized.");
        }

        _balance.SetCost(userModel.ModelId, usage.Usage.InputTokens, usage.Usage.OutputTokens, usage.Usage.CacheTokens, _priceConfig);
        if (!_balance.IsSufficient)
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

    private (ChatTokenUsage usage, bool isReliable) EnsureFinalUsage(ChatRequest chatRequest, UserModel userModel)
    {
        if (_priceConfig == null || _balance == null)
        {
            throw new InvalidOperationException("InChatContext has not been initialized.");
        }

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
                    CacheCreationTokens = baseUsage.CacheCreationTokens
                }, false);
            }
            else
            {
                return (baseUsage, true);
            }
        }
        else
        {
            int promptTokens = chatRequest.EstimatePromptTokens(ChatService.Tokenizer);
            return (additionalUsage with
            {
                InputTokens = promptTokens,
            }, false);
        }
    }

    private static ChatTokenUsage CalculateUsageFromSegments(IEnumerable<ChatSegment> segments)
    {
        int textTokens = segments
            .OfType<TextChatSegment>()
            .Sum(t => ChatService.Tokenizer.CountTokens(t.Text));
        int reasoningTokens = segments
            .OfType<ThinkChatSegment>()
            .Sum(t => ChatService.Tokenizer.CountTokens(t.Think));

        return new ChatTokenUsage
        {
            InputTokens = 0,
            OutputTokens = textTokens + reasoningTokens,
            ReasoningTokens = reasoningTokens,
            CacheTokens = 0,
        };
    }

    public ChatCompletionSnapshot? FullResponse { get; private set; }

    public int ReasoningDurationMs => _segments.OfType<ThinkChatSegment>().Any()
        ? (int)Stopwatch.GetElapsedTime(_firstReasoningTick, _firstResponseTick).TotalMilliseconds
        : 0;

    public UserModelUsage ToUserModelUsage(int userId, ScopedBalanceCalculator calc, UserModel userModel, int clientInfoId, bool isApi)
    {
        if (_finishTick == _preprocessTick) _finishTick = Stopwatch.GetTimestamp();

        ChatCompletionSnapshot snapshot = FullResponse ?? throw new InvalidOperationException("FullResponse is not available.");

        UserModelUsage usage = new()
        {
            ModelId = userModel.ModelId,
            Model = userModel.Model,
            UserId = userModel.UserId,
            User = userModel.User,
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
            InputFreshCost = calc.Cost.InputFreshCost,
            OutputCost = calc.Cost.OutputCost,
            InputCachedCost = calc.Cost.InputCachedCost,
            ClientInfoId = clientInfoId,
        };

        byte transactionTypeId = (byte)(isApi ? DBTransactionType.ApiCost : DBTransactionType.Cost);
        if (calc.Cost.TotalCost > 0)
        {
            usage.BalanceTransaction = new()
            {
                UserId = userId,
                CreatedAt = usage.CreatedAt,
                CreditUserId = userId,
                Amount = -calc.Cost.TotalCost,
                TransactionTypeId = transactionTypeId,
            };
        }
        if (calc.Cost.UsageInfo.TryGetValue(userModel.ModelId, out BalanceInitialUsageInfo? usageInfo) && (usageInfo.Counts > 0 || usageInfo.Tokens > 0))
        {
            usage.UsageTransaction = new()
            {
                ModelId = userModel.ModelId,
                CreditUserId = userId,
                CreatedAt = usage.CreatedAt,
                CountAmount = -usageInfo.Counts,
                TokenAmount = -usageInfo.Tokens,
                TransactionTypeId = transactionTypeId,
            };
        }

        return usage;
    }

    public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(firstTick, Stopwatch.GetTimestamp());
}
