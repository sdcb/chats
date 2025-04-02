using Chats.BE.Services.Models.ChatServices;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.Dtos;

public record ChatSegment
{
    public required ChatFinishReason? FinishReason { get; init; }

    public required ICollection<ChatSegmentItem> Items { get; init; }

    public required ChatTokenUsage? Usage { get; init; }

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
