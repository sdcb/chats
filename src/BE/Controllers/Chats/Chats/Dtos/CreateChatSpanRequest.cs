using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record CreateChatSpanRequest
{
    [JsonPropertyName("modelId")]
    public short ModelId { get; init; }
}
