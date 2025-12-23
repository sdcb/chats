using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 模型的用户权限列表项DTO - 用于模型视角管理用户权限
/// </summary>
public record ModelUserPermissionDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    /// <summary>
    /// 邮箱
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// 电话
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 是否已分配该模型
    /// </summary>
    [JsonPropertyName("isAssigned")]
    public required bool IsAssigned { get; init; }

    /// <summary>
    /// UserModel ID (如果已分配)
    /// </summary>
    [JsonPropertyName("userModelId")]
    public int? UserModelId { get; init; }

    /// <summary>
    /// 使用次数余额
    /// </summary>
    [JsonPropertyName("counts")]
    public int? Counts { get; init; }

    /// <summary>
    /// Token余额
    /// </summary>
    [JsonPropertyName("tokens")]
    public int? Tokens { get; init; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("expires")]
    public DateTime? Expires { get; init; }
}
