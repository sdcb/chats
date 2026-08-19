using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record UpdateMcpServerRequest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("headers")] public string? Headers { get; init; }
    [JsonPropertyName("serverInstructions")] public string? ServerInstructions { get; init; }
    [JsonPropertyName("tools")] public required List<McpToolBasicInfo> Tools { get; init; }

    public bool ValidateToolNameUnique()
    {
        if (Tools.Count == 0) return true;

        if (Tools.Count != Tools.Select(t => t.Name).Distinct().Count())
            return false;

        return true;
    }
}
