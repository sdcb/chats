using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record ResourceLimitResponse(
    [property: JsonPropertyName("defaultValue")] double DefaultValue,
    [property: JsonPropertyName("maxValue")] double MaxValue
);

