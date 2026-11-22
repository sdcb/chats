namespace Chats.BE.Controllers.Users.Containers.Dtos;

public sealed record ContainerPortMappingDto(
    long Id,
    int ContainerPort,
    int HostPort,
    string Protocol,
    int RuntimeNodeId,
    string PublicHost,
    string UpstreamAddress,
    DateTime CreatedAt);

public sealed record CreateContainerPortMappingRequest(int ContainerPort, string? Protocol);
