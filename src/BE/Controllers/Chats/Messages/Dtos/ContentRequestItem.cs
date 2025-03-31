using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextContentRequestItem), typeDiscriminator: 0)]
[JsonDerivedType(typeof(FileContentRequestItem), typeDiscriminator: 1)]
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
