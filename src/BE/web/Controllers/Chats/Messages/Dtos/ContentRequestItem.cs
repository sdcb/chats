using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

public record EditUserMessageRequest
{
    [JsonPropertyName("contents")]
    public required ContentRequestItem[] Contents { get; init; }
}

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

    public static ContentRequestItem FromDB(StepContent content, IUrlEncryptionService urlEncryption)
    {
        return (DBStepContentType)content.ContentTypeId switch
        {
            DBStepContentType.Text => new TextContentRequestItem
            {
                Text = content.StepContentText!.Content,
                ContextTemplate = content.StepContentText.ContextTemplate,
            },
            DBStepContentType.FileId => new FileContentRequestItem { FileId = urlEncryption.EncryptFileId(content.StepContentFile!.FileId) },
            _ => throw new NotSupportedException(),
        };
    }

    private readonly static DBStepContentType[] AllowedContentTypes = 
    [
        DBStepContentType.Text,
        DBStepContentType.FileId,
    ];

    public static ContentRequestItem[] FromDB(ICollection<StepContent> contents, IUrlEncryptionService urlEncryption)
    {
        return [.. contents
            .Where(x => AllowedContentTypes.Contains((DBStepContentType)x.ContentTypeId))
            .Select(content => FromDB(content, urlEncryption))];
    }

    public static ContentRequestItem[] FromDB(ICollection<StepContent> contents, IUrlEncryptionService urlEncryption, long targetContentId, TextContentRequestItem replacementText)
    {
        return [.. contents
            .Where(x => AllowedContentTypes.Contains((DBStepContentType)x.ContentTypeId))
            .Select(content => content.Id switch
            {
                var x when x == targetContentId => replacementText with
                {
                    ContextTemplate = content.StepContentText?.ContextTemplate,
                },
                _ => FromDB(content, urlEncryption),
            })];
    }
}

public record TextContentRequestItem : ContentRequestItem
{
    [JsonPropertyName("c")]
    public required string Text { get; init; }

    [JsonIgnore]
    public string? ContextTemplate { get; init; }

    public override Task<StepContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return Task.FromResult(StepContent.FromText(Text, ContextTemplate));
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
