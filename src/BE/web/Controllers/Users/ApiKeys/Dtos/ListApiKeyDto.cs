namespace Chats.Web.Controllers.Users.ApiKeys.Dtos;

using System.Text.Json.Serialization;

public record ListApiKeyDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("isRevoked")]
    public required bool IsRevoked { get; init; }

    [JsonPropertyName("comment")]
    public required string? Comment { get; init; }

    [JsonPropertyName("allowEnumerate")]
    public required bool AllowEnumerate { get; init; }

    [JsonPropertyName("allowAllModels")]
    public required bool AllowAllModels { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }

    [JsonPropertyName("lastUsedAt")]
    public required DateTime? LastUsedAt { get; init; }

    [JsonPropertyName("modelCount")]
    public required int ModelCount { get; init; }
}