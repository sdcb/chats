using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record McpServerDetailsDto : ManagementMcpServerDto
{
    [JsonPropertyName("headers")] public string? Headers { get; init; }
    [JsonPropertyName("tools")] public required List<McpToolBasicInfo> Tools { get; init; }
}
