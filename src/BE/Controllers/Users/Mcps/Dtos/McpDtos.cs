using Chats.BE.DB;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record McpToolBasicInfo
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("parameters")] public string? Parameters { get; init; }

    public McpTool ToDB()
    {
        return new McpTool
        {
            ToolName = Name,
            Description = Description,
            Parameters = Parameters
        };
    }
}
