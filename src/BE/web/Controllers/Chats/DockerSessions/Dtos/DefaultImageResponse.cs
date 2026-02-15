using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record DefaultImageResponse(
    [property: JsonPropertyName("defaultImage")] string DefaultImage,
    [property: JsonPropertyName("description")] string? Description
);

