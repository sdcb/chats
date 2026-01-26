using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed class RunCommandRequest
{
    [JsonPropertyName("command")]
    [Required]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }
}

