using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.ApiKeys.Dtos;

public record CreateApiKeyDto
{
    [JsonPropertyName("comment")]
    public required string Comment { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }
}
