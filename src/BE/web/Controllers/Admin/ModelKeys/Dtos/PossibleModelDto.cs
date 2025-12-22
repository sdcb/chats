using System.Text.Json.Serialization;
using Chats.Web.Controllers.Admin.AdminModels.Dtos;

namespace Chats.Web.Controllers.Admin.ModelKeys.Dtos;

public record PossibleModelDto
{
    [JsonPropertyName("deploymentName")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("existingModel")]
    public AdminModelDto? ExistingModel { get; init; }
}
