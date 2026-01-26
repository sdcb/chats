using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record TextFileResponse(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("isText")] bool IsText,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("text")] string? Text
);

