using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record McpToolDto : McpToolBasicInfo
{
    [JsonPropertyName("requireApproval")]
    public required bool RequireApproval { get; init; }
}