using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed class DeleteFileRequest
{
    [JsonPropertyName("path")]
    [Required]
    public string Path { get; set; } = string.Empty;
}
