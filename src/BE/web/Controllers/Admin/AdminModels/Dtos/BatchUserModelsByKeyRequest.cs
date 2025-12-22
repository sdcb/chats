using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 按Key批量操作用户模型请求（添加/删除）
/// </summary>
public record BatchUserModelsByKeyRequest
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    /// <summary>
    /// Model Key ID
    /// </summary>
    [JsonPropertyName("keyId")]
    public required int KeyId { get; init; }
}
