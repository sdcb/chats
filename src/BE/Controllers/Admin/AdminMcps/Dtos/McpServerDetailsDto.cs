using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record McpServerDetailsDto : McpServerListItemDto
{
    [JsonPropertyName("headers")] public string? Headers { get; init; }
    [JsonPropertyName("tools")] public required McpToolDto[] Tools { get; init; }
}
