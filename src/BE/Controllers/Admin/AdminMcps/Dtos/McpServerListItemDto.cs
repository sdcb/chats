using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record McpServerListItemDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("label")] public required string Label { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("createdAt")] public required DateTime CreatedAt { get; init; }
    [JsonPropertyName("lastFetchAt")] public DateTime? LastFetchAt { get; init; }
    [JsonPropertyName("toolsCount")] public required int ToolsCount { get; init; }
}

public record ManagementMcpServerDto : McpServerListItemDto
{
    [JsonPropertyName("isSystem")] public required bool IsSystem { get; init; }
    [JsonPropertyName("owner")] public required string Owner { get; init; }
    [JsonPropertyName("editable")] public required bool Editable { get; init; }

}