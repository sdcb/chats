using System.Text.Json.Serialization;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;

namespace Chats.BE.Controllers.Admin.ModelKeys.Dtos;

public record PossibleModelDto
{
    [JsonPropertyName("deploymentName")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("existingModel")]
    public AdminModelDto? ExistingModel { get; init; }
}
