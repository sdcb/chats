using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record EnvironmentVariablesResponse(
    [property: JsonPropertyName("systemVariables")] IReadOnlyList<EnvironmentVariable> SystemVariables,
    [property: JsonPropertyName("userVariables")] IReadOnlyList<EnvironmentVariable> UserVariables
);

public sealed record EnvironmentVariable(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value
);
