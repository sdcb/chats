using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

[JsonDerivedType(typeof(EditableMcpServerDetailsDto))]
public record McpServerDetailsDto : ManagementMcpServerDto
{
    [JsonPropertyName("serverInstructions")] public string? ServerInstructions { get; init; }
    [JsonPropertyName("tools")] public required List<McpToolBasicInfo> Tools { get; init; }
}

public record EditableMcpServerDetailsDto : McpServerDetailsDto
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("headers")] public string? Headers { get; init; }
}
