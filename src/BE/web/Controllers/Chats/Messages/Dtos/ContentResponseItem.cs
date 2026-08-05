using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

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
        => FromContent(content, fup, urlEncryption, null);

    private static ContentResponseItem FromContent(
        StepContent content,
        FileUrlProvider fup,
        IUrlEncryptionService urlEncryption,
        IReadOnlyDictionary<string, string>? toolNames)
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
                Parameters = content.StepContentToolCall.Name == DeepSeekHostedWebSearch.InternalToolName
                    ? DeepSeekHostedWebSearch.CreatePresentationCall(content.StepContentToolCall.Parameters)
                    : content.StepContentToolCall.Parameters,
            },
            DBStepContentType.ToolCallResponse => new ToolCallResponseItem()
            {
                Id = encryptedMessageContentId,
                ToolCallId = content.StepContentToolCallResponse!.ToolCallId!,
                Response = toolNames?.GetValueOrDefault(content.StepContentToolCallResponse!.ToolCallId)
                    == DeepSeekHostedWebSearch.InternalToolName
                    || DeepSeekHostedWebSearch.TryParseBlock(
                        content.StepContentToolCallResponse.Response,
                        DeepSeekHostedWebSearch.ToolResultType,
                        out _)
                    ? DeepSeekHostedWebSearch.CreatePresentationResponse(content.StepContentToolCallResponse.Response)
                    : content.StepContentToolCallResponse.Response,
            },
            _ => throw new NotSupportedException(),
        };
    }

    public static ContentResponseItem[] FromContent(StepContent[] contents, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        Dictionary<string, string> toolNames = new(StringComparer.Ordinal);
        foreach (StepContent content in contents.Where(x =>
            x.ContentTypeId == (byte)DBStepContentType.ToolCall
            && x.StepContentToolCall != null))
        {
            toolNames[content.StepContentToolCall!.ToolCallId] = content.StepContentToolCall.Name;
        }
        return [.. contents.Select(x => FromContent(x, fup, urlEncryption, toolNames))];
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
