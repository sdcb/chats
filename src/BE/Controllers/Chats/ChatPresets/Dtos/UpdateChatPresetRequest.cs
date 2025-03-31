using Chats.BE.Controllers.Chats.Chats.Dtos;

namespace Chats.BE.Controllers.Chats.ChatPresets.Dtos;

public record UpdateChatPresetRequest
{
    public required string Name { get; init; }

    public required UpdateChatSpanRequest[] Spans { get; init; }
}
