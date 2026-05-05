using System.Text.Json.Serialization;
using Chats.BE.Controllers.Admin.AdminModels.Validators;

namespace Chats.BE.Controllers.Admin.ModelKeys.Dtos;

public record UpdateModelKeyRequest
{
    [JsonPropertyName("modelProviderId")]
    public required short ModelProviderId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("secret")]
    public string? Secret { get; init; }

    [JsonPropertyName("customHeaders")]
    [ValidateCustomHeaders]
    public string? CustomHeaders { get; init; }

    [JsonPropertyName("customBody")]
    [ValidateCustomBodyPatch]
    public string? CustomBody { get; init; }
}
