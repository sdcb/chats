using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record FetchToolsRequest(string ServerUrl, string? Headers);

public record FetchToolsResponse
{
    [JsonPropertyName("tools")] public required List<McpToolBasicInfo> Tools { get; init; }
    [JsonPropertyName("serverInstructions")] public string? ServerInstructions { get; init; }
}