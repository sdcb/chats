using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record UpdateMcpServerRequest
{
    [JsonPropertyName("label")] public required string Label { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("requireApproval")] public bool RequireApproval { get; init; }
    [JsonPropertyName("headers")] public string? Headers { get; init; }
    [JsonPropertyName("isPublic")] public bool IsPublic { get; init; }
}
