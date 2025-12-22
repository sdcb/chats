using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.ModelProviders.Dtos;

public record ModelProviderDto
{
    [JsonPropertyName("providerId")]
    public required short ProviderId { get; init; }

    [JsonPropertyName("keyCount")]
    public required int KeyCount { get; init; }

    [JsonPropertyName("modelCount")]
    public required int ModelCount { get; init; }
}
