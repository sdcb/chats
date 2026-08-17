using Chats.DB;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

public record McpToolBasicInfo
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("parameters")] public string? Parameters { get; init; }
    [JsonPropertyName("destructive")] public bool Destructive { get; init; }
    [JsonPropertyName("idempotent")] public bool Idempotent { get; init; }
    [JsonPropertyName("openWorld")] public bool OpenWorld { get; init; }
    [JsonPropertyName("readOnly")] public bool ReadOnly { get; init; }

    public McpTool ToDB()
    {
        return new McpTool
        {
            ToolName = Name,
            Title = GetNormalizedTitle(),
            Description = Description,
            Parameters = Parameters,
            Destructive = Destructive,
            Idempotent = Idempotent,
            OpenWorld = OpenWorld,
            ReadOnly = ReadOnly,
        };
    }

    public string? GetNormalizedTitle()
        => string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
}
