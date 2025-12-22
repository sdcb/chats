using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Prompts.Dtos;

public record BriefPromptDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    [JsonPropertyName("isSystem")]
    public required bool IsSystem { get; init; }
}