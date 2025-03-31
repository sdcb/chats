using System.Text.Json.Serialization;

namespace Chats.BE.Services.Configs;

public record SiteInfo
{
    [JsonPropertyName("customizedLine1")]
    public string? CustomizedLine1 { get; init; }

    [JsonPropertyName("customizedLine2")]
    public string? CustomizedLine2 { get; init; }
}
