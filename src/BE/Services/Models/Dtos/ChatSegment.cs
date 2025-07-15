using OpenAI.Chat;
using OpenAI.Responses;

namespace Chats.BE.Services.Models.Dtos;

public record ChatSegment
{
    public required ChatFinishReason? FinishReason { get; init; }

    public required ICollection<ChatSegmentItem> Items { get; init; }

    public required ChatTokenUsage? Usage { get; init; }

    public static ChatSegment FromTextOnly(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromText(text)],
            Usage = null,
        };
    }

    public static ChatSegment FromThinkOnly(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromThink(text)],
            Usage = null,
        };
    }

    public static ChatSegment FromStartToolCall(StreamingResponseOutputItemAddedUpdate delta, FunctionCallResponseItem fc)
    {
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromToolCall(delta, fc)],
            Usage = null,
        };
    }

    public static ChatSegment FromToolCall(int fcIndex, FunctionCallResponseItem fc)
    {
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromToolCall(fcIndex, fc)],
            Usage = null,
        };
    }

    public static ChatSegment FromToolCallDelta(StreamingResponseFunctionCallArgumentsDeltaUpdate delta)
    {
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromToolCallDelta(delta)],
            Usage = null,
        };
    }

    public static ChatSegment Completed(ChatTokenUsage usage, ChatFinishReason? finishReason)
    {
        ArgumentNullException.ThrowIfNull(usage, nameof(usage));
        return new ChatSegment
        {
            FinishReason = finishReason,
            Items = [],
            Usage = usage,
        };
    }

    public InternalChatSegment ToInternal(Func<ChatTokenUsage> usageCalculator)
    {
        if (Usage is not null)
        {
            return new InternalChatSegment
            {
                Usage = Usage,
                FinishReason = FinishReason,
                Items = Items,
                IsUsageReliable = true,
                IsFromUpstream = true,
            };
        }
        else
        {
            return new InternalChatSegment
            {
                Usage = Usage ?? usageCalculator(),
                FinishReason = FinishReason,
                Items = Items,
                IsUsageReliable = false,
                IsFromUpstream = true,
            };
        }
    }
}
