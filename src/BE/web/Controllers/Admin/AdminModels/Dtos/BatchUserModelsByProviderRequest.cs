using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 按Provider批量操作用户模型请求（添加/删除）
/// </summary>
public record BatchUserModelsByProviderRequest
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    /// <summary>
    /// Model Provider ID
    /// </summary>
    [JsonPropertyName("providerId")]
    public required int ProviderId { get; init; }
}
