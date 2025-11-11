using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 按模型批量添加/删除用户模型请求DTO
/// </summary>
public record BatchUserModelsByModelRequest
{
    /// <summary>
    /// 模型ID
    /// </summary>
    [JsonPropertyName("modelId")]
    [Required]
    public required int ModelId { get; init; }

    /// <summary>
    /// 用户ID列表
    /// </summary>
    [JsonPropertyName("userIds")]
    [Required]
    public required List<int> UserIds { get; init; }
}
