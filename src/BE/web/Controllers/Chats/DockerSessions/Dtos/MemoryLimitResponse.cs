using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record MemoryLimitResponse(
    [property: JsonPropertyName("defaultBytes")] long DefaultBytes,
    [property: JsonPropertyName("maxBytes")] long MaxBytes
);

