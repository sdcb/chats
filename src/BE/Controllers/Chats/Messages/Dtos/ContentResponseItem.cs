using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(ErrorContentResponseItem), typeDiscriminator: 0)]
[JsonDerivedType(typeof(TextContentResponseItem), typeDiscriminator: 1)]
[JsonDerivedType(typeof(FileResponseItem), typeDiscriminator: 2)]
[JsonDerivedType(typeof(ReasoningResponseItem), typeDiscriminator: 3)]
public abstract record ContentResponseItem
{
    public static ContentResponseItem FromSegment(MessageContent segment, FileUrlProvider fup)
    {
        return (DBMessageContentType)segment.ContentTypeId switch
        {
            DBMessageContentType.Text => new TextContentResponseItem()
            {
                Content = segment.MessageContentText!.Content
            },
            DBMessageContentType.Error => new ErrorContentResponseItem()
            {
                Content = segment.MessageContentText!.Content
            },
            DBMessageContentType.Reasoning => new ReasoningResponseItem()
            {
                Content = segment.MessageContentText!.Content
            },
            DBMessageContentType.FileId => new FileResponseItem()
            {
                Content = fup.CreateFileDto(segment.MessageContentFile!.File)
            },
            _ => throw new NotSupportedException(),
        };
    }

    public static ContentResponseItem[] FromSegment(MessageContent[] segments, FileUrlProvider fup)
    {
        return [.. segments.Select(x => FromSegment(x, fup))];
    }
}

public record TextContentResponseItem : ContentResponseItem
{
    [JsonPropertyName("c")]
    public required string Content { get; init; }
}

public record ErrorContentResponseItem : ContentResponseItem
{
    [JsonPropertyName("c")]
    public required string Content { get; init; }
}

public record ReasoningResponseItem : ContentResponseItem
{
    [JsonPropertyName("c")]
    public required string Content { get; init; }
}

public record FileResponseItem : ContentResponseItem
{
    [JsonPropertyName("c")]
    public required FileDto Content { get; init; }
}