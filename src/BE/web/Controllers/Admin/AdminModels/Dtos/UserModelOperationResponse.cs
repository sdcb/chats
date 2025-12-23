using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 用户模型操作响应（包含更新后的统计信息）
/// </summary>
public record UserModelOperationResponse
{
    /// <summary>
    /// 操作影响的记录数
    /// </summary>
    [JsonPropertyName("affectedCount")]
    public required int AffectedCount { get; init; }

    /// <summary>
    /// 更新后的用户模型总数
    /// </summary>
    [JsonPropertyName("userModelCount")]
    public required int UserModelCount { get; init; }

    /// <summary>
    /// 更新后的 Provider 统计（如果操作涉及 Provider）
    /// </summary>
    [JsonPropertyName("providerStats")]
    public UserModelProviderDto? ProviderStats { get; init; }

    /// <summary>
    /// 更新后的 Key 统计（如果操作涉及 Key）
    /// </summary>
    [JsonPropertyName("keyStats")]
    public UserModelKeyDto? KeyStats { get; init; }

    /// <summary>
    /// 更新后的模型列表（如果是单个模型操作）
    /// </summary>
    [JsonPropertyName("model")]
    public UserModelPermissionModelDto? Model { get; init; }
}
