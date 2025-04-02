using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextContentRequestItem), typeDiscriminator: (int)DBMessageContentType.Text)]
[JsonDerivedType(typeof(FileContentRequestItem), typeDiscriminator: (int)DBMessageContentType.FileId)]
public abstract record ContentRequestItem
{
    public abstract Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken);

    public static async Task<MessageContent[]> ToMessageContents(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToMessageContent(fup, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static ContentRequestItem FromDB(MessageContent mc, IUrlEncryptionService idEncryption)
    {
        return (DBMessageContentType)mc.ContentTypeId switch
        {
            DBMessageContentType.Text => new TextContentRequestItem { Text = mc.MessageContentText!.Content },
            DBMessageContentType.FileId => new FileContentRequestItem { FileId = idEncryption.EncryptFileId(mc.MessageContentFile!.FileId) },
            _ => throw new NotSupportedException(),
        };
    }

    public static ContentRequestItem[] FromDB(ICollection<MessageContent> mcs, IUrlEncryptionService idEncryption)
    {
        return [.. mcs
            .Where(x => x.ContentTypeId == (byte)DBMessageContentType.Text || x.ContentTypeId == (byte)DBMessageContentType.FileId)
            .Select(mc => FromDB(mc, idEncryption))];
    }

    public static ContentRequestItem[] FromDB(ICollection<MessageContent> mcs, IUrlEncryptionService idEncryption, long patchContentId, TextContentRequestItem patchText)
    {
        return [.. mcs
            .Where(x => x.ContentTypeId == (byte)DBMessageContentType.Text || x.ContentTypeId == (byte)DBMessageContentType.FileId)
            .Select(mc => mc.Id switch 
            {
                var x when x == patchContentId => patchText,
                _ => FromDB(mc, idEncryption),
            })];
    }
}

public record TextContentRequestItem : ContentRequestItem
{
    [JsonPropertyName("c")]
    public required string Text { get; init; }

    public override Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return Task.FromResult(MessageContent.FromContent(Text));
    }
}

public record FileContentRequestItem : ContentRequestItem
{
    [JsonPropertyName("c")]
    public required string FileId { get; init; }

    public override async Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await fup.CreateFileContent(FileId, cancellationToken);
    }
}

[Obsolete("Use ContentRequestItem instead")]
public record MessageContentRequest
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("fileIds")]
    public List<string>? FileIds { get; init; }

    public async Task<MessageContent[]> ToMessageContents(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return
        [
            MessageContent.FromContent(Text),
            ..(await (FileIds ?? [])
                .ToAsyncEnumerable()
                .SelectAwait(async fileId => await fup.CreateFileContent(fileId, cancellationToken))
                .ToArrayAsync(cancellationToken)),
        ];
    }

    public ContentRequestItem[] ToRequestItem()
    {
        return
        [
            new TextContentRequestItem { Text = Text },
            ..(FileIds ?? []).Select(fileId => new FileContentRequestItem { FileId = fileId }),
        ];
    }
}
