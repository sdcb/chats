using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Containers.Dtos;

public sealed record CreateContainerResourceRequest(
    string? Name,
    bool IsPermanent,
    int TemplateId,
    string? Image,
    float? CpuCores,
    long? MemoryBytes,
    int? MaxProcesses,
    string? BackendNetworkName,
    string? OwnerChatId);

public sealed record UpdateContainerResourceRequest(
    float? CpuCores,
    long? MemoryBytes,
    int? MaxProcesses,
    string? BackendNetworkName);

public sealed record ContainerResourceDto(
    string EncryptedId,
    string Name,
    bool IsPermanent,
    string Image,
    float? CpuCores,
    long? MemoryBytes,
    int? MaxProcesses,
    string? BackendNetworkName,
    string? RuntimeNodeAIName,
    string? Ip,
    bool IsDeleted,
    bool IsStopped,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CleanupAt,
    IReadOnlyList<string> GrantedChatIds,
    long? OwnerTurnId);

public sealed record UserContainerResourceDto(
    long Id,
    string EncryptedId,
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

public record UserContainerResourceQuery : PagingRequest
{
    [FromQuery(Name = "id")]
    public string? Id { get; init; }

    [FromQuery(Name = "query")]
    public string? Query { get; init; }

    [FromQuery(Name = "runtimeNodeId")]
    public int? RuntimeNodeId { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "permanent")]
    public bool? Permanent { get; init; }
}

public record UserContainerResourceExportQuery
{
    [FromQuery(Name = "id")]
    public string? Id { get; init; }

    [FromQuery(Name = "query")]
    public string? Query { get; init; }

    [FromQuery(Name = "runtimeNodeId")]
    public int? RuntimeNodeId { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "permanent")]
    public bool? Permanent { get; init; }

    [FromQuery(Name = "columns")]
    public string? Columns { get; init; }
}

public sealed record ResourceErrorDto(string Code, string Message);

public sealed record ContainerTemplateDto(
    int Id,
    string Name,
    int RuntimeNodeId,
    string RuntimeNodeAIName,
    string Image,
    float CpuCores,
    long MemoryBytes,
    int MaxProcesses,
    string? BackendNetworkName,
    long? DefaultVolumeBytes,
    byte Visibility);
