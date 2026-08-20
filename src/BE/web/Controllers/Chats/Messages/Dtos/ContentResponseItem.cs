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
                DisplayName = content.StepContentToolCall!.DisplayName,
                ToolCallId = content.StepContentToolCall!.ToolCallId!,
                Parameters = CreateToolCallPresentation(content.StepContentToolCall),
            },
            DBStepContentType.ToolCallResponse => new ToolCallResponseItem()
            {
                Id = encryptedMessageContentId,
                ToolCallId = content.StepContentToolCallResponse!.ToolCallId!,
                Response = DeepSeekHostedWebSearch.TryCreatePresentationResponse(
                    content.StepContentToolCallResponse.Response,
                    out string presentationResponse)
                    ? presentationResponse
                    : content.StepContentToolCallResponse.Response,
            },
            _ => throw new NotSupportedException(),
        };

        static string CreateToolCallPresentation(StepContentToolCall toolCall)
        {
            return toolCall.Name == DeepSeekHostedWebSearch.InternalToolName
                && DeepSeekHostedWebSearch.TryCreatePresentationCall(toolCall.Parameters, out string presentationCall)
                    ? presentationCall
                    : toolCall.Parameters;
        }
    }

    public static ContentResponseItem[] FromContent(StepContent[] contents, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        // Signature-only Think blocks are required for subsequent model context, so they remain
        // in the database and are still consumed by model conversions. They are not user-visible
        // reasoning, however, so omit empty Think items only from the response DTO sent to the UI.
        return [.. contents
            .Where(x => (DBStepContentType)x.ContentTypeId != DBStepContentType.Think
                || !string.IsNullOrWhiteSpace(x.StepContentThink?.Content))
            .Select(x => FromContent(x, fup, urlEncryption))];
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

    [JsonPropertyName("d")]
    public string? DisplayName { get; init; }

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
