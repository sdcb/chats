using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record McpServerListItemDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("showShortcut")] public bool ShowShortcut { get; init; }
}

public record ManagementMcpServerDto : McpServerListItemDto
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("createdAt")] public required DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public required DateTime UpdatedAt { get; init; }
    [JsonPropertyName("toolsCount")] public required int ToolsCount { get; init; }
    [JsonPropertyName("owner")] public required string Owner { get; init; }
    [JsonPropertyName("editable")] public required bool Editable { get; init; }
    [JsonPropertyName("assignedUserCount")] public required int AssignedUserCount { get; init; }
    /// <summary>Whether the current user is assigned this MCP (can toggle own shortcut).</summary>
    [JsonPropertyName("assignedToMe")] public bool AssignedToMe { get; init; }
}
