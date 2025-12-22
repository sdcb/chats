using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Users.Mcps.Dtos;

public record AssignUserMcpsRequest
{
    [JsonPropertyName("userId")] public required int UserId { get; init; }
    [JsonPropertyName("mcpServerIds")] public required int[] McpServerIds { get; init; }

    internal bool HasDuplicateMcpServerIds() => McpServerIds.Distinct().Count() != McpServerIds.Length;
}
