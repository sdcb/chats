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
public sealed record RuntimeNodeDto(int Id, string Name, string AIName, string? Description, byte BackendType, string? Endpoint, bool IsEnabled);
public sealed record ImageRequest(string? Description, bool IsEnabled);
public sealed record QuotaRequest(bool AllowCustomImage, string AllowedNetworkModes, int? MaxContainerCount, float? MaxCpuCores, long? MaxMemoryBytes, int? MaxContainerProcesses, long? MaxVolumeBytes, float? MaxContainerCpuCores, long? MaxContainerMemoryBytes, long? MaxVolumeBytesPerVolume);
public sealed record TemplateRequest(string Name, int RuntimeNodeId, string Image, float CpuCores, long MemoryBytes, int MaxProcesses, string? BackendNetworkName, long? DefaultVolumeBytes, byte Visibility);
