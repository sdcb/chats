using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record McpToolDto
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("parameters")] public string? Parameters { get; init; }
}
