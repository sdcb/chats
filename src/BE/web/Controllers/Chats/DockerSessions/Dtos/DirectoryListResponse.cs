using Chats.DockerInterface.Models;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

public sealed record DirectoryListResponse(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("entries")] IReadOnlyList<FileEntry> Entries
);

