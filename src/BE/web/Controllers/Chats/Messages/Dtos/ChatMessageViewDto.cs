using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

public sealed record ChatMessageViewDto
{
    [JsonPropertyName("messages")]
    public required TurnDto[] Messages { get; init; }

    [JsonPropertyName("leafMessageId")]
    public required string? LeafMessageId { get; init; }
}
