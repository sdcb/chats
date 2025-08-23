using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMcps.Dtos;

public record AssignUserMcpRequest
{
    [JsonPropertyName("userId")] public required int UserId { get; init; }
    [JsonPropertyName("mcpServerIds")] public required int[] McpServerIds { get; init; }

    internal bool HasDuplicateMcpServerIds() => McpServerIds.Distinct().Count() != McpServerIds.Length;
}
