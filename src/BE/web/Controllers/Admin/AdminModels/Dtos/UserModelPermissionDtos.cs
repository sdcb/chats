using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.AdminModels.Dtos;

/// <summary>
/// 用户模型权限 - 模型提供商DTO
/// </summary>
public record UserModelProviderDto
{
    /// <summary>
    /// 提供商ID
    /// </summary>
    [JsonPropertyName("providerId")]
    public required int ProviderId { get; init; }

    /// <summary>
    /// 模型密钥数量
    /// </summary>
    [JsonPropertyName("keyCount")]
    public required int KeyCount { get; init; }

    /// <summary>
    /// 模型总数
    /// </summary>
    [JsonPropertyName("modelCount")]
    public required int ModelCount { get; init; }

    /// <summary>
    /// 已分配的模型数量
    /// </summary>
    [JsonPropertyName("assignedModelCount")]
    public required int AssignedModelCount { get; init; }
}

/// <summary>
/// 用户模型权限 - 模型密钥DTO
/// </summary>
public record UserModelKeyDto
{
    /// <summary>
    /// 密钥ID
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>
    /// 密钥名称
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 模型总数
    /// </summary>
    [JsonPropertyName("modelCount")]
    public required int ModelCount { get; init; }

    /// <summary>
    /// 已分配的模型数量
    /// </summary>
    [JsonPropertyName("assignedModelCount")]
    public required int AssignedModelCount { get; init; }
}

/// <summary>
/// 用户模型权限 - 模型DTO
/// </summary>
public record UserModelPermissionModelDto
{
    /// <summary>
    /// 模型ID
    /// </summary>
    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    /// <summary>
    /// 模型名称
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// 是否已分配
    /// </summary>
    [JsonPropertyName("isAssigned")]
    public required bool IsAssigned { get; init; }

    /// <summary>
    /// 用户模型ID（如果已分配）
    /// </summary>
    [JsonPropertyName("userModelId")]
    public int? UserModelId { get; init; }

    /// <summary>
    /// 调用次数余额（如果已分配）
    /// </summary>
    [JsonPropertyName("counts")]
    public int? Counts { get; init; }

    /// <summary>
    /// 令牌余额（如果已分配）
    /// </summary>
    [JsonPropertyName("tokens")]
    public int? Tokens { get; init; }

    /// <summary>
    /// 过期时间（如果已分配）
    /// </summary>
    [JsonPropertyName("expires")]
    public DateTime? Expires { get; init; }

    /// <summary>
    /// 是否已删除
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public required bool IsDeleted { get; init; }
}
