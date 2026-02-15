using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed class CreateChatDockerSessionRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("cpuCores")]
    public double? CpuCores { get; set; }

    [JsonPropertyName("memoryBytes")]
    public long? MemoryBytes { get; set; }

    [JsonPropertyName("maxProcesses")]
    public long? MaxProcesses { get; set; }

    [JsonPropertyName("networkMode")]
    public string? NetworkMode { get; set; }
}
