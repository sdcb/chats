using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.Models.ChatServices;

[JsonPolymorphic]
[JsonDerivedType(typeof(Base64Image), typeDiscriminator: "base64")]
[JsonDerivedType(typeof(UrlImage), typeDiscriminator: "url")]
public abstract record ImageChatSegment : ChatSegmentItem
{
    public abstract Task<DBFileDef> Download(CancellationToken cancellationToken = default);

    public abstract string ToTempUrl();

    public abstract string ToContentType();
}

public record Base64Image : ImageChatSegment
{
    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("base64")]
    public required string Base64 { get; init; }

    public override Task<DBFileDef> Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = Convert.FromBase64String(Base64);
        return Task.FromResult(new DBFileDef(bytes, ContentType, null));
    }

    public override string ToTempUrl() => $"data:{ContentType};base64,{Base64}";

    public override string ToContentType() => ContentType;
}

public record UrlImage : ImageChatSegment
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    public override async Task<DBFileDef> Download(CancellationToken cancellationToken = default)
    {
        using HttpClient client = new();
        HttpResponseMessage resp = await client.GetAsync(Url, cancellationToken);
        resp.EnsureSuccessStatusCode();

        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
        string contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string? fileName = resp.Content.Headers.ContentDisposition?.FileName;
        return new DBFileDef(bytes, contentType, fileName);
    }

    public override string ToTempUrl() => Url;

    public override string ToContentType() => Url switch
    {
        var x when x.Contains(".png", StringComparison.OrdinalIgnoreCase) => "image/png",
        var x when x.Contains(".jpg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
        var x when x.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
        var x when x.Contains(".gif", StringComparison.OrdinalIgnoreCase) => "image/gif",
        var x when x.Contains(".webp", StringComparison.OrdinalIgnoreCase) => "image/webp",
        var x when x.Contains(".bmp", StringComparison.OrdinalIgnoreCase) => "image/bmp",
        var x when x.Contains(".tiff", StringComparison.OrdinalIgnoreCase) => "image/tiff",
        var x when x.Contains(".svg", StringComparison.OrdinalIgnoreCase) => "image/svg+xml",
        var x when x.Contains(".ico", StringComparison.OrdinalIgnoreCase) => "image/vnd.microsoft.icon",
        var x when x.Contains(".heic", StringComparison.OrdinalIgnoreCase) => "image/heic",
        var x when x.Contains(".avif", StringComparison.OrdinalIgnoreCase) => "image/avif",
        _ => "application/octet-stream"
    };
}