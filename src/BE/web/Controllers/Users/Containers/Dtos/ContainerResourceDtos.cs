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
