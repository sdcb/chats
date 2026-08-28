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
