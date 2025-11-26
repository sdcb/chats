using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Api.OpenAICompatible.Dtos;

public class ModelListItemDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public required string Object { get; init; }

    [JsonPropertyName("created")]
    public required long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; init; }
}

public record ModelListDto
{
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    [JsonPropertyName("data")]
    public required ModelListItemDto[] Data { get; init; }
}