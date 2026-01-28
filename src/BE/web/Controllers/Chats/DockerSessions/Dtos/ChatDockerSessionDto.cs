using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record ChatDockerSessionDto(
    [property: JsonPropertyName("encryptedSessionId")] string EncryptedSessionId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("containerId")] string ContainerId,
    [property: JsonPropertyName("cpuCores")] float? CpuCores,
    [property: JsonPropertyName("memoryBytes")] long? MemoryBytes,
    [property: JsonPropertyName("maxProcesses")] short? MaxProcesses,
    [property: JsonPropertyName("networkMode")] string NetworkMode,
    [property: JsonPropertyName("ipAddress")] string? IpAddress,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("lastActiveAt")] DateTime LastActiveAt,
    [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt
);

