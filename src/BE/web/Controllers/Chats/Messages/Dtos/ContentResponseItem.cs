using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(ErrorContentResponseItem), typeDiscriminator: (int)DBStepContentType.Error)]
[JsonDerivedType(typeof(TextContentResponseItem), typeDiscriminator: (int)DBStepContentType.Text)]
[JsonDerivedType(typeof(FileResponseItem), typeDiscriminator: (int)DBStepContentType.FileId)]
[JsonDerivedType(typeof(ReasoningResponseItem), typeDiscriminator: (int)DBStepContentType.Think)]
[JsonDerivedType(typeof(ToolCallingResponseItem), typeDiscriminator: (int)DBStepContentType.ToolCall)]
[JsonDerivedType(typeof(ToolCallResponseItem), typeDiscriminator: (int)DBStepContentType.ToolCallResponse)]
public abstract record ContentResponseItem
{
    [JsonPropertyName("i")]
    public required string Id { get; init; }

    public static ContentResponseItem FromContent(StepContent content, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        string encryptedMessageContentId = urlEncryption.EncryptMessageContentId(content.Id);
        return (DBStepContentType)content.ContentTypeId switch
        {
            DBStepContentType.Text => new TextContentResponseItem()
            {
                Id = encryptedMessageContentId, 
                Content = content.StepContentText!.Content
            },
            DBStepContentType.Error => new ErrorContentResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.StepContentText!.Content
            },
            DBStepContentType.Think => new ReasoningResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = content.StepContentThink!.Content
            },
            DBStepContentType.FileId => new FileResponseItem()
            {
                Id = encryptedMessageContentId,
                Content = fup.CreateFileDto(content.StepContentFile!.File)
            },
            DBStepContentType.ToolCall => new ToolCallingResponseItem()
            {
                Id = encryptedMessageContentId,
                Name = content.StepContentToolCall!.Name,
                ToolCallId = content.StepContentToolCall!.ToolCallId!,
                Parameters = content.StepContentToolCall!.Parameters,
            },
            DBStepContentType.ToolCallResponse => new ToolCallResponseItem()
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