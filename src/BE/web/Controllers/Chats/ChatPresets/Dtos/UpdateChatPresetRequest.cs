using Chats.Web.Controllers.Chats.Chats.Dtos;

namespace Chats.Web.Controllers.Chats.ChatPresets.Dtos;

public record UpdateChatPresetRequest
{
    public required string Name { get; init; }

    public required UpdateChatSpanRequest[] Spans { get; init; }
}
