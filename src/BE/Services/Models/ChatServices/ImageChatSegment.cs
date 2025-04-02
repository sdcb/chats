using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
using System.Text.Json.Serialization;
using File = Chats.BE.DB.File;

namespace Chats.BE.Services.Models.ChatServices;

[JsonPolymorphic]
[JsonDerivedType(typeof(Base64Image), typeDiscriminator: "base64")]
[JsonDerivedType(typeof(UrlImage), typeDiscriminator: "url")]
public abstract record ImageChatSegment : ChatSegmentItem
{
    public override async Task<MessageContent> ToDB(DBFileService fs, CancellationToken cancellationToken = default)
    {
        DBFileDef def = await Download(cancellationToken);
        File file = await fs.StoreNoSave(def, cancellationToken: cancellationToken);

        return new MessageContent()
        {
            ContentTypeId = (byte)DBMessageContentType.FileId,
            MessageContentFile = new()
            {
                File = file
            }
        };
    }

    public abstract Task<DBFileDef> Download(CancellationToken cancellationToken = default);
}

public record Base64Image : ImageChatSegment
{
    public required string ContentType { get; init; }

    public required string Base64 { get; init; }

    public override Task<DBFileDef> Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = Convert.FromBase64String(Base64);
        return Task.FromResult(new DBFileDef(bytes, ContentType, null));
    }
}

public record UrlImage : ImageChatSegment
{
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
}