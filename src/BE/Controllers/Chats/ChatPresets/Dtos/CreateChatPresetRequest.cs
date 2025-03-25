namespace Chats.BE.Controllers.Chats.ChatPresets.Dtos;

public record CreateChatPresetRequest
{
    public required string Name { get; init; }
}
