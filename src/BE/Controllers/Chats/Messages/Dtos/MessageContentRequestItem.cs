using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextContentRequestItem), typeDiscriminator: 0)]
[JsonDerivedType(typeof(FileContentRequestItem), typeDiscriminator: 1)]
public abstract record MessageContentRequestItem
{
    public abstract Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken);

    public static async Task<MessageContent[]> ToMessageContents(MessageContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToMessageContent(fup, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }
}

public record TextContentRequestItem : MessageContentRequestItem
{
    [JsonPropertyName("c")]
    public required string Text { get; init; }

    public override Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return Task.FromResult(MessageContent.FromContent(Text));
    }
}

public record FileContentRequestItem : MessageContentRequestItem
{
    [JsonPropertyName("c")]
    public required string FileId { get; init; }

    public override async Task<MessageContent> ToMessageContent(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await fup.CreateFileContent(FileId, cancellationToken);
    }
}
