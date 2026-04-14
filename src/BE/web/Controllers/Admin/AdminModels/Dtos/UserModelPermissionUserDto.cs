using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 用户模型权限列表项DTO - 用于用户模型权限管理页面
/// </summary>
public record UserModelPermissionUserDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    /// <summary>
    /// 帐号
    /// </summary>
    [JsonPropertyName("account")]
    public required string Account { get; init; }

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
    /// 登录提供方
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 已分配的模型数量
    /// </summary>
    [JsonPropertyName("userModelCount")]
    public required int UserModelCount { get; init; }

    /// <summary>
    /// 系统可用的模型提供商数量
    /// </summary>
    [JsonPropertyName("modelProviderCount")]
    public required int ModelProviderCount { get; init; }
}

public record UserModelPermissionUserQuery : PagingRequest
{
    [FromQuery(Name = "id")]
    public string? Id { get; init; }

    [FromQuery(Name = "username")]
    public string? Username { get; init; }

    [FromQuery(Name = "phone")]
    public string? Phone { get; init; }

    [FromQuery(Name = "email")]
    public string? Email { get; init; }

    [FromQuery(Name = "loginType")]
    public string? LoginType { get; init; }
}
