using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record McpServerListItemDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("label")] public required string Label { get; init; }
}

public record ManagementMcpServerDto : McpServerListItemDto
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("createdAt")] public required DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public required DateTime UpdatedAt { get; init; }
    [JsonPropertyName("toolsCount")] public required int ToolsCount { get; init; }
    [JsonPropertyName("owner")] public required string Owner { get; init; }
    [JsonPropertyName("editable")] public required bool Editable { get; init; }

}