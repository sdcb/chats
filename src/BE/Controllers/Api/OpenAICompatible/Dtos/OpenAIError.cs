using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Api.OpenAICompatible.Dtos;

public record ErrorResponse
{
    [JsonPropertyName("error")]
    public required ErrorDetail Error { get; init; }
}

public record ErrorDetail
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("param")]
    public required string? Param { get; init; }

    [JsonPropertyName("code")]
    public required string Code { get; init; }
}