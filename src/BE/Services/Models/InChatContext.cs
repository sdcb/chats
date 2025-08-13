using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.DB.Jsons;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using System.Diagnostics;

namespace Chats.BE.Services.Models;

public class InChatContext(long firstTick)
{
    private long _preprocessTick, _firstReasoningTick, _firstResponseTick, _endResponseTick, _finishTick;
    private short _segmentCount;
    private InternalChatSegment _lastSegment = InternalChatSegment.Empty;
    private readonly List<ChatSegmentItem> _items = [];

    public DBFinishReason FinishReason { get; set; } = DBFinishReason.Success;

    public async IAsyncEnumerable<InternalChatSegment> Run(ScopedBalanceCalculator balance, UserModel userModel, IAsyncEnumerable<InternalChatSegment> segments)
    {
        _preprocessTick = _firstReasoningTick = _firstResponseTick = _endResponseTick = _finishTick = Stopwatch.GetTimestamp();
        if (userModel.ExpiresAt.IsExpired())
        {
            throw new SubscriptionExpiredException(userModel.ExpiresAt);
        }
        JsonPriceConfig priceConfig = userModel.Model.ToPriceConfig();
        if (!balance.IsSufficient)
        {
            throw new InsufficientBalanceException();
        }

        balance.SetCost(userModel.ModelId, 0, 0, priceConfig);
        if (!balance.IsSufficient)
        {
            throw new InsufficientBalanceException();
        }

        try
        {
            _preprocessTick = _firstReasoningTick = _firstResponseTick = _endResponseTick = _finishTick = Stopwatch.GetTimestamp();
            await foreach (InternalChatSegment seg in segments)
            {
                if (seg.IsFromUpstream)
                {
                    _segmentCount++;
                    string? think = seg.Items.GetThink();
                    if (!string.IsNullOrEmpty(think))
                    {
                        if (_firstReasoningTick == _preprocessTick) // never reasoning
                        {
                            _firstReasoningTick = Stopwatch.GetTimestamp();
                        }
                    }
                    if (seg.Items.OfType<TextChatSegment>().Any() || seg.Items.OfType<ImageChatSegment>().Any() || seg.Items.OfType<ToolCallSegment>().Any())
                    {
                        if (_firstResponseTick == _preprocessTick) // never response
                        {
                            _firstResponseTick = Stopwatch.GetTimestamp();
                        }
                    }
                }
                _lastSegment = seg;
                _items.AddOne(seg.Items);

                balance.SetCost(userModel.ModelId, seg.Usage.InputTokens, seg.Usage.OutputTokens, priceConfig);
                if (!balance.IsSufficient)
                {
                    FinishReason = DBFinishReason.InsufficientBalance;
                    throw new InsufficientBalanceException();
                }
                FinishReason = seg.ToDBFinishReason() ?? FinishReason;

                yield return seg;
            }
        }
        finally
        {
            _endResponseTick = Stopwatch.GetTimestamp();
        }
    }

    public InternalChatSegment FullResponse => _lastSegment with { Items = _items, };

    public int ReasoningDurationMs => _items.OfType<ThinkChatSegment>().Any() ? (int)Stopwatch.GetElapsedTime(_firstReasoningTick, _firstResponseTick).TotalMilliseconds : 0;

    public UserModelUsage ToUserModelUsage(int userId, ScopedBalanceCalculator calc, UserModel userModel, int clientInfoId, bool isApi)
    {
        if (_finishTick == _preprocessTick) _finishTick = Stopwatch.GetTimestamp();

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
            InputTokens = _lastSegment.Usage.InputTokens,
            OutputTokens = _lastSegment.Usage.OutputTokens,
            ReasoningTokens = _lastSegment.Usage.ReasoningTokens,
            IsUsageReliable = _lastSegment.IsUsageReliable,
            InputCost = calc.Cost.InputCost,
            OutputCost = calc.Cost.OutputCost,
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