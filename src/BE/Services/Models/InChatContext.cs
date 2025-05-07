using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.DB.Jsons;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models.Dtos;
using System.Diagnostics;

namespace Chats.BE.Services.Models;

public class InChatContext(long firstTick)
{
    private long _preprocessTick, _firstReasoningTick, _firstResponseTick, _endResponseTick, _finishTick;
    private short _segmentCount;
    public UserModelBalanceCost Cost { get; private set; } = UserModelBalanceCost.Empty;
    private InternalChatSegment _lastSegment = InternalChatSegment.Empty;
    private readonly List<ChatSegmentItem> _items = [];

    public DBFinishReason FinishReason { get; set; } = DBFinishReason.Success;

    public async IAsyncEnumerable<InternalChatSegment> Run(decimal userBalance, UserModel userModel, IAsyncEnumerable<InternalChatSegment> segments)
    {
        _preprocessTick = _firstReasoningTick = _firstResponseTick = _endResponseTick = _finishTick = Stopwatch.GetTimestamp();
        if (userModel.ExpiresAt.IsExpired())
        {
            throw new SubscriptionExpiredException(userModel.ExpiresAt);
        }
        JsonPriceConfig priceConfig = userModel.Model.ToPriceConfig();
        if (userModel.TokenBalance == 0 && userModel.CountBalance == 0 && userBalance == 0 && !priceConfig.IsFree())
        {
            throw new InsufficientBalanceException();
        }

        UserModelBalanceCalculator calculator = new(userModel.CountBalance, userModel.TokenBalance, userBalance);
        Cost = calculator.GetNewBalance(0, 0, priceConfig);
        if (!Cost.IsSufficient)
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
                    string? text = seg.Items.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (_firstResponseTick == _preprocessTick) // never response
                        {
                            _firstResponseTick = Stopwatch.GetTimestamp();
                        }
                    }
                }
                _lastSegment = seg;
                _items.AddOne(seg.Items);

                UserModelBalanceCost currentCost = calculator.GetNewBalance(seg.Usage.InputTokens, seg.Usage.OutputTokens, priceConfig);
                if (!currentCost.IsSufficient)
                {
                    FinishReason = DBFinishReason.InsufficientBalance;
                    throw new InsufficientBalanceException();
                }
                Cost = currentCost;
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

    public UserModelUsage ToUserModelUsage(int userId, UserModel userModel, int clientInfoId, bool isApi)
    {
        if (_finishTick == _preprocessTick) _finishTick = Stopwatch.GetTimestamp();

        UserModelUsage usage = new()
        {
            UserModelId = userModel.Id,
            UserModel = userModel,
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
            InputCost = Cost.InputTokenPrice,
            OutputCost = Cost.OutputTokenPrice,
            ClientInfoId = clientInfoId,
        };

        byte transactionTypeId = (byte)(isApi ? DBTransactionType.ApiCost : DBTransactionType.Cost);
        if (Cost.CostBalance > 0)
        {
            usage.BalanceTransaction = new()
            {
                UserId = userId,
                CreatedAt = usage.CreatedAt,
                CreditUserId = userId,
                Amount = -Cost.CostBalance,
                TransactionTypeId = transactionTypeId,
            };
        }
        if (Cost.CostCount > 0 || Cost.CostTokens > 0)
        {
            usage.UsageTransaction = new()
            {
                UserModelId = userModel.Id,
                CreditUserId = userId,
                CreatedAt = usage.CreatedAt,
                CountAmount = -Cost.CostCount,
                TokenAmount = -Cost.CostTokens,
                TransactionTypeId = transactionTypeId,
            };
        }

        return usage;
    }

    public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(firstTick, Stopwatch.GetTimestamp());
}