using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(ErrorContentResponseItem), typeDiscriminator: (int)DBMessageContentType.Error)]
[JsonDerivedType(typeof(TextContentResponseItem), typeDiscriminator: (int)DBMessageContentType.Text)]
[JsonDerivedType(typeof(FileResponseItem), typeDiscriminator: (int)DBMessageContentType.FileId)]
[JsonDerivedType(typeof(ReasoningResponseItem), typeDiscriminator: (int)DBMessageContentType.Reasoning)]
[JsonDerivedType(typeof(ToolCallingResponseItem), typeDiscriminator: (int)DBMessageContentType.ToolCall)]
[JsonDerivedType(typeof(ToolCallResponseItem), typeDiscriminator: (int)DBMessageContentType.ToolCallResponse)]
public abstract record ContentResponseItem
{
    [JsonPropertyName("i")]
    public required string Id { get; init; }

    public static ContentResponseItem FromContent(StepContent content, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        string encryptedMessageContentId = urlEncryption.EncryptMessageContentId(content.Id);
        return (DBMessageContentType)content.ContentTypeId switch
        {
            DBMessageContentType.Text => new TextContentResponseItem()
            {
                Id = encryptedMessageContentId, 
                Content = content.StepContentText!.Content
            },
            DBMessageContentType.Error => new ErrorContentResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.StepContentText!.Content
            },
            DBMessageContentType.Reasoning => new ReasoningResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.StepContentText!.Content
            },
            DBMessageContentType.FileId => new FileResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = fup.CreateFileDto(content.StepContentFile!.File)
            },
            DBMessageContentType.ToolCall => new ToolCallingResponseItem()
            {
                Id = encryptedMessageContentId,
                Name = content.StepContentToolCall!.Name,
                ToolCallId = content.StepContentToolCall!.ToolCallId!,
                Parameters = content.StepContentToolCall!.Parameters,
            },
            DBMessageContentType.ToolCallResponse => new ToolCallResponseItem()
            {
                Id = encryptedMessageContentId,
                ToolCallId = content.StepContentToolCallResponse!.ToolCallId!,
                Response = content.StepContentToolCallResponse!.Response,
            },
            _ => throw new NotSupportedException(),
        };
    }

    public static ContentResponseItem[] FromContent(StepContent[] contents, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
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

public record ToolCallingResponseItem : ContentResponseItem
{
    [JsonPropertyName("u")]
    public required string ToolCallId { get; init; }

    [JsonPropertyName("n")]
    public required string Name { get; init; }

    [JsonPropertyName("p")]
    public required string Parameters { get; init; }
}

public record ToolCallResponseItem : ContentResponseItem
{
    [JsonPropertyName("u")]
    public required string ToolCallId { get; init; }

    [JsonPropertyName("r")]
    public required string Response { get; init; }
}