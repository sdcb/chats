using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;

public record GlobalConfigDto
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("description")]
    public required string? Description { get; init; }
}
