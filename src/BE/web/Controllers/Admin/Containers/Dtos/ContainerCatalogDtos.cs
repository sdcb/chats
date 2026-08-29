using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.Containers.Dtos;

public sealed record RuntimeNodeRequest(
    string Name,
    string AIName,
    string? Description,
    byte BackendType,
    string? Endpoint,
    string? Credential,
    bool IsEnabled);

public sealed record EnabledRequest(bool IsEnabled);
public sealed record RuntimeNodeDto(int Id, string Name, string AIName, string? Description, byte BackendType, string? Endpoint, bool HasCredential, bool IsEnabled, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record RuntimeNodeQuery(string? Query, byte? BackendType, bool? Enabled);
public sealed record RuntimeNodeExportQuery(string? Query, byte? BackendType, bool? Enabled, string? Columns);
public sealed record ContainerResourceTemplateDto(
    int Id,
    string Name,
    int RuntimeNodeId,
    string Image,
    float CpuCores,
    long MemoryBytes,
    int MaxProcesses,
    string? BackendNetworkName,
    long? DefaultVolumeBytes,
    byte Visibility,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    RuntimeNodeDto? RuntimeNode);
public sealed record TemplateQuery(string? Query, int? RuntimeNodeId, byte? Visibility);
public sealed record TemplateExportQuery(string? Query, int? RuntimeNodeId, byte? Visibility, string? Columns);
public sealed record ContainerQuotaDto(
    int Id,
    int? UserId,
    string? UserName,
    bool AllowCustomImage,
    string AllowedNetworkModes,
    int? MaxContainerCount,
    float? MaxCpuCores,
    long? MaxMemoryBytes,
    int? MaxContainerProcesses,
    long? MaxVolumeBytes,
    float? MaxContainerCpuCores,
    long? MaxContainerMemoryBytes,
    long? MaxVolumeBytesPerVolume,
    DateTime UpdatedAt);
public sealed record ContainerImageDto(int Id, string Image, string? Description, bool IsEnabled);
public sealed record ImageQuery(string? Query, bool? Enabled);
public sealed record ImageExportQuery(string? Query, bool? Enabled, string? Columns);
public sealed record QuotaQuery(string? Query, bool? AllowCustomImage, string? Scope);
public sealed record QuotaExportQuery(string? Query, bool? AllowCustomImage, string? Scope, string? Columns);

public sealed record ContainerResourceAdminDto(
    long Id,
    int OwnerUserId,
    string? OwnerUserName,
    string? OwnerDisplayName,
    int? OwnerChatId,
    string? OwnerChatTitle,
    long? OwnerTurnId,
    int RuntimeNodeId,
    string? RuntimeNodeName,
    string? RuntimeNodeAIName,
    bool IsPermanent,
    string BackendResourceId,
    string? Ip,
    string Name,
    string Image,
    string? ShellPrefix,
    float? CpuCores,
    long? MemoryBytes,
    int? MaxProcesses,
    string? BackendNetworkName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastActiveAt,
    DateTime? StoppedAt,
    DateTime? DeletedAt,
    DateTime? CleanupAt,
    long? VolumeDeclaredBytes,
    int VolumeMountCount,
    int ChatAccessCount);

public record ContainerResourceQuery : PagingRequest
{
    [FromQuery(Name = "id")]
    public string? Id { get; init; }

    [FromQuery(Name = "query")]
    public string? Query { get; init; }

    [FromQuery(Name = "owner")]
    public string? Owner { get; init; }

    [FromQuery(Name = "runtimeNodeId")]
    public int? RuntimeNodeId { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "permanent")]
    public bool? Permanent { get; init; }
}

public record ContainerResourceExportQuery
{
    [FromQuery(Name = "id")]
    public string? Id { get; init; }

    [FromQuery(Name = "query")]
    public string? Query { get; init; }

    [FromQuery(Name = "owner")]
    public string? Owner { get; init; }

    [FromQuery(Name = "runtimeNodeId")]
    public int? RuntimeNodeId { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "permanent")]
    public bool? Permanent { get; init; }

    [FromQuery(Name = "columns")]
    public string? Columns { get; init; }
}

public sealed record ImageRequest(string Image, string? Description, bool IsEnabled);
public sealed record QuotaRequest(bool AllowCustomImage, string AllowedNetworkModes, int? MaxContainerCount, float? MaxCpuCores, long? MaxMemoryBytes, int? MaxContainerProcesses, long? MaxVolumeBytes, float? MaxContainerCpuCores, long? MaxContainerMemoryBytes, long? MaxVolumeBytesPerVolume);
public sealed record TemplateRequest(string Name, int RuntimeNodeId, string Image, float CpuCores, long MemoryBytes, int MaxProcesses, string? BackendNetworkName, long? DefaultVolumeBytes, byte Visibility);
