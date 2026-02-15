using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed class SaveUserEnvironmentVariablesRequest
{
    [JsonPropertyName("variables")]
    [Required]
    public IReadOnlyList<EnvironmentVariable> Variables { get; set; } = [];
}
