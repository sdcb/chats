using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record NetworkModesResponse(
    [property: JsonPropertyName("defaultNetworkMode")] string DefaultNetworkMode,
    [property: JsonPropertyName("maxAllowedNetworkMode")] string MaxAllowedNetworkMode,
    [property: JsonPropertyName("allowedNetworkModes")] IReadOnlyList<string> AllowedNetworkModes
);

