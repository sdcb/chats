using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.DB.Jsons;

public record JsonInitialMcp
{
    [JsonPropertyName("mcpServerId")]
    public required int McpServerId { get; init; }

    [JsonPropertyName("showShortcut")]
    public required bool ShowShortcut { get; init; }

    [JsonPropertyName("customHeaders")]
    public string? CustomHeaders { get; init; }

    public bool HasValidCustomHeaders()
    {
        if (string.IsNullOrWhiteSpace(CustomHeaders))
        {
            return true;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(CustomHeaders);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public JsonInitialMcp Normalize()
        => this with
        {
            CustomHeaders = string.IsNullOrWhiteSpace(CustomHeaders) ? null : CustomHeaders.Trim()
        };
}
