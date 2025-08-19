using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Linq;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextContentRequestItem), typeDiscriminator: (int)DBMessageContentType.Text)]
[JsonDerivedType(typeof(FileContentRequestItem), typeDiscriminator: (int)DBMessageContentType.FileId)]
public abstract record ContentRequestItem
{
    public abstract Task<StepContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken);

    public static async Task<StepContent[]> ToMessageContents(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToMessageContent(fup, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static ContentRequestItem FromDB(StepContent mc, IUrlEncryptionService idEncryption)
    {
        return (DBMessageContentType)mc.ContentTypeId switch
        {
            DBMessageContentType.Text => new TextContentRequestItem { Text = mc.StepContentText!.Content },
            DBMessageContentType.FileId => new FileContentRequestItem { FileId = idEncryption.EncryptFileId(mc.StepContentFile!.FileId) },
            _ => throw new NotSupportedException(),
        };
    }

    private readonly static DBMessageContentType[] AllowedContentTypes = 
    [
        DBMessageContentType.Text,
        DBMessageContentType.FileId,
    ];

    public static ContentRequestItem[] FromDB(ICollection<StepContent> mcs, IUrlEncryptionService idEncryption)
    {
        return [.. mcs
            .Where(x => AllowedContentTypes.Contains((DBMessageContentType)x.ContentTypeId))
            .Select(mc => FromDB(mc, idEncryption))];
    }

    public static ContentRequestItem[] FromDB(ICollection<StepContent> mcs, IUrlEncryptionService idEncryption, long patchContentId, TextContentRequestItem patchText)
    {
        return [.. mcs
            .Where(x => AllowedContentTypes.Contains((DBMessageContentType)x.ContentTypeId))
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

    public override Task<StepContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepContent.FromText(Text));
    }
}

public record FileContentRequestItem : ContentRequestItem
{
    [JsonPropertyName("c")]
    public required string FileId { get; init; }

    public override async Task<StepContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
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

    public async Task<StepContent[]> ToMessageContents(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return
        [
            StepContent.FromText(Text),
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
