using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record ImageListResponse(
    [property: JsonPropertyName("images")] IReadOnlyList<string> Images
);

