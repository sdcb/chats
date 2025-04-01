using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(ErrorContentResponseItem), typeDiscriminator: 0)]
[JsonDerivedType(typeof(TextContentResponseItem), typeDiscriminator: 1)]
[JsonDerivedType(typeof(FileResponseItem), typeDiscriminator: 2)]
[JsonDerivedType(typeof(ReasoningResponseItem), typeDiscriminator: 3)]
public abstract record ContentResponseItem
{
    [JsonPropertyName("i")]
    public required string Id { get; init; }

    public static ContentResponseItem FromContent(MessageContent content, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        string encryptedMessageContentId = urlEncryption.EncryptMessageContentId(content.Id);
        return (DBMessageContentType)content.ContentTypeId switch
        {
            DBMessageContentType.Text => new TextContentResponseItem()
            {
                Id = encryptedMessageContentId, 
                Content = content.MessageContentText!.Content
            },
            DBMessageContentType.Error => new ErrorContentResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.MessageContentText!.Content
            },
            DBMessageContentType.Reasoning => new ReasoningResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.MessageContentText!.Content
            },
            DBMessageContentType.FileId => new FileResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = fup.CreateFileDto(content.MessageContentFile!.File)
            },
            _ => throw new NotSupportedException(),
        };
    }

    public static ContentResponseItem[] FromContent(MessageContent[] contents, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return [.. contents.Select(x => FromContent(x, fup, urlEncryption))];
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