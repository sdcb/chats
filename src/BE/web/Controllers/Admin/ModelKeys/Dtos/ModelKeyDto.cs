using Chats.Web.Services.Common;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.ModelKeys.Dtos;

public record ModelKeyDto
{
    [JsonPropertyName("id")]
    public required short Id { get; init; }

    [JsonPropertyName("modelProviderId")]
    public required short ModelProviderId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("enabledModelCount")]
    public required int EnabledModelCount { get; init; }

    [JsonPropertyName("totalModelCount")]
    public required int TotalModelCount { get; init; }

    [JsonPropertyName("host")]
    public required string? Host { get; init; }

    [JsonPropertyName("secret")]
    public required string? Secret { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    public ModelKeyDto WithMaskedKeys()
    {
        return this with { Secret = Secret.JsonToMaskedNull() };
    }
}
