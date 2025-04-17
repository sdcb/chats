using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;

public record CheckUpdateResponse
{
    [JsonPropertyName("hasNewVersion")]
    public required bool HasNewVersion { get; init; }

    [JsonPropertyName("tagName")]
    public required string? LatestVersion { get; init; }

    [JsonPropertyName("currentVersion")]
    public required string? CurrentVersion { get; init; }
}
