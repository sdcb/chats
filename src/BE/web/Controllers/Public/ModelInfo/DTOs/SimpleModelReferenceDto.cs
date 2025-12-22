using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Public.ModelInfo.DTOs;

public record SimpleModelReferenceDto
{
    [JsonPropertyName("id")]
    public required short Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}