namespace Chats.BE.Controllers.Users.Containers.Dtos;

public sealed record CreateVolumeRequest(
    int RuntimeNodeId,
    string Name,
    string? BackendVolumeId,
    long? DeclaredBytes);

public sealed record MountVolumeRequest(
    string EncryptedContainerResourceId,
    string ContainerPath,
    bool IsReadOnly);

public sealed record ContainerVolumeDto(
    long Id,
    string Name,
    int RuntimeNodeId,
    string? RuntimeNodeName,
    string? BackendVolumeId,
    long? DeclaredBytes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ContainerVolumeMountDto> ContainerVolumeMounts);

public sealed record ContainerVolumeMountDto(
    long Id,
    long ContainerResourceId,
    string EncryptedContainerResourceId,
    string? ContainerName,
    string ContainerPath,
    bool IsReadOnly,
    DateTime MountedAt);
