using Chats.BE.Services.Models.ChatServices;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.Dtos;

public record ChatSegment
{
    public required ChatFinishReason? FinishReason { get; init; }

    public required ChatSegmentItem[] Segments { get; init; }

    public required ChatTokenUsage? Usage { get; init; }

    public InternalChatSegment ToInternal(Func<ChatTokenUsage> usageCalculator)
    {
        if (Usage is not null)
        {
            return new InternalChatSegment
            {
                Usage = Usage,
                FinishReason = FinishReason,
                Segments = Segments,
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
                Segments = Segments,
                IsUsageReliable = false,
                IsFromUpstream = true,
            };
        }
    }
}
