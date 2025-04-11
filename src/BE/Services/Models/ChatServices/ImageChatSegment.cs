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
}