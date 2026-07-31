using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Mcps.Dtos;

// 用于分配用户的请求
public record AssignUsersToMcpRequest
{
    [JsonPropertyName("toAssignedUsers")] public List<AssignedUserInfo> ToAssignedUsers { get; init; } = [];
    [JsonPropertyName("toUpdateUsers")] public List<AssignedUserInfo> ToUpdateUsers { get; init; } = [];
    [JsonPropertyName("toDeleteUserIds")] public List<int> ToDeleteUserIds { get; init; } = [];
}

// 分配用户信息
public record AssignedUserInfo
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("customHeaders")] public string? CustomHeaders { get; init; }
    [JsonPropertyName("showShortcut")] public bool? ShowShortcut { get; init; }
}

// 未分配用户信息
public record UnassignedUserDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("userName")] public required string UserName { get; init; }
}

// 已分配用户详细信息
public record AssignedUserDetailsDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("userName")] public required string UserName { get; init; }
    [JsonPropertyName("customHeaders")] public string? CustomHeaders { get; init; }
    [JsonPropertyName("showShortcut")] public required bool ShowShortcut { get; init; }
}

// 当前用户更新自己的 MCP 赋权偏好
public record UpdateMyMcpAssignmentRequest
{
    [JsonPropertyName("showShortcut")] public required bool ShowShortcut { get; init; }
}

// 用于快速获取用户名的DTO
public record AssignedUserNameDto
{
    [JsonPropertyName("id")] public required int Id { get; init; }
    [JsonPropertyName("userName")] public required string UserName { get; init; }
}