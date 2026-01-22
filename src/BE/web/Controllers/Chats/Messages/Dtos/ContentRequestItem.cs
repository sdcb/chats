using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextContentRequestItem), typeDiscriminator: (int)DBStepContentType.Text)]
[JsonDerivedType(typeof(FileContentRequestItem), typeDiscriminator: (int)DBStepContentType.FileId)]
public abstract record ContentRequestItem
{
    public abstract Task<StepContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken);

    public static async Task<StepContent[]> ToMessageContents(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .Select(async (item, ct) => await item.ToMessageContent(fup, ct))
            .ToArrayAsync(cancellationToken);
    }

    public static ContentRequestItem FromDB(StepContent mc, IUrlEncryptionService idEncryption)
    {
        return (DBStepContentType)mc.ContentTypeId switch
        {
            DBStepContentType.Text => new TextContentRequestItem { Text = mc.StepContentText!.Content },
            DBStepContentType.FileId => new FileContentRequestItem { FileId = idEncryption.EncryptFileId(mc.StepContentFile!.FileId) },
            _ => throw new NotSupportedException(),
        };
    }

    private readonly static DBStepContentType[] AllowedContentTypes = 
    [
        DBStepContentType.Text,
        DBStepContentType.FileId,
    ];

    public static ContentRequestItem[] FromDB(ICollection<StepContent> mcs, IUrlEncryptionService idEncryption)
    {
        return [.. mcs
            .Where(x => AllowedContentTypes.Contains((DBStepContentType)x.ContentTypeId))
            .Select(mc => FromDB(mc, idEncryption))];
    }

    public static ContentRequestItem[] FromDB(ICollection<StepContent> mcs, IUrlEncryptionService idEncryption, long patchContentId, TextContentRequestItem patchText)
    {
        return [.. mcs
            .Where(x => AllowedContentTypes.Contains((DBStepContentType)x.ContentTypeId))
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
                .Select(async (fileId, ct) => await fup.CreateFileContent(fileId, ct))
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
