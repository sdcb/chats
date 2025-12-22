using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Public.ModelInfo.DTOs;

public record ModelProviderDto
{
    [JsonPropertyName("id")]
    public required short Id { get; init; }

    [JsonPropertyName("modelReferences")]
    public required SimpleModelReferenceDto[] ModelReferences { get; init; }
}

public record InitialModelKeyConfigDto
{
    [JsonPropertyName("initialHost")]
    public required string? InitialHost { get; init; }

    [JsonPropertyName("initialSecret")]
    public required string? InitialSecret { get; init; }
}