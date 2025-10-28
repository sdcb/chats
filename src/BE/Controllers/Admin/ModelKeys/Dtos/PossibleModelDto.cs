using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.ModelKeys.Dtos;

public record PossibleModelDto
{
    [JsonPropertyName("deploymentName")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("isExists")]
    public required bool IsExists { get; init; }
}
