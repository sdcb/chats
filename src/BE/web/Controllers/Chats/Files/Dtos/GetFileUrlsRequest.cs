using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Files.Dtos;

public record GetFileUrlsRequest
{
    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }

    [JsonPropertyName("fileType")]
    public required string FileType { get; init; }

    public override string ToString() => $"{FileName}.{FileType}";
}
