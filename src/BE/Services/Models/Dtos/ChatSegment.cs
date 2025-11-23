using Chats.BE.Services.Models.ChatServices;
using OpenAI.Chat;
using OpenAI.Responses;

namespace Chats.BE.Services.Models.Dtos;

public record ChatSegment
{
    public required ChatFinishReason? FinishReason { get; init; }

    public required ICollection<ChatSegmentItem> Items { get; init; }

    public required ChatTokenUsage? Usage { get; init; }

    public static ChatSegment FromUsageOnly(int inputToken, int outputToken, int reasoningToken = 0)
    {
        return new ChatSegment
        {
            FinishReason = null,
            Items = [],
            Usage = new ()
            {
                InputTokens = inputToken,
                OutputTokens = outputToken,
                ReasoningTokens = reasoningToken,
            },
        };
    }

    public static ChatSegment FromText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromText(text)],
            Usage = null,
        };
    }

    public static ChatSegment FromThinking(string thinking)
    {
        ArgumentException.ThrowIfNullOrEmpty(thinking, nameof(thinking));
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromThink(thinking)],
            Usage = null,
        };
    }

    public static ChatSegment FromThinkingSignature(string signature)
    {
        ArgumentException.ThrowIfNullOrEmpty(signature, nameof(signature));
        return new ChatSegment
        {
            FinishReason = null,
            Items = [ChatSegmentItem.FromThinkingSegment(signature)],
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

    public static ChatSegment FromToolCall(ToolCallSegment toolCallSegment)
    {
        return new ChatSegment
        {
            FinishReason = null,
            Items = [toolCallSegment],
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
