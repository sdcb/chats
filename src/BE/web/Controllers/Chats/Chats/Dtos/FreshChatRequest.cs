using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public abstract record EncryptedWebChatRequest
{
    [JsonPropertyName("chatId")]
    public required string EncryptedChatId { get; init; }

    [JsonPropertyName("timezoneOffset")]
    public required short TimezoneOffset { get; init; }

    public abstract WebChatRequest Decrypt(IUrlEncryptionService urlEncryption);
}

public record EncryptedRegenerateAssistantMessageRequest : EncryptedWebChatRequest
{
    [JsonPropertyName("parentUserMessageId")]
    public required string ParentUserMessageId { get; init; }

    [JsonPropertyName("spanId")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    public override WebChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new RegenerateAssistantMessageRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            ParentUserMessageId = urlEncryption.DecryptTurnId(ParentUserMessageId),
            SpanId = SpanId,
            ModelId = ModelId
        };
    }
}

public record EncryptedRegenerateAllAssistantMessageRequest : EncryptedWebChatRequest
{
    [JsonPropertyName("parentUserMessageId")]
    public required string ParentUserMessageId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    public override WebChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new RegenerateAllAssistantMessageRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            ParentUserMessageId = urlEncryption.DecryptTurnId(ParentUserMessageId),
            ModelId = ModelId
        };
    }
}

public record EncryptedGeneralChatRequest : EncryptedWebChatRequest
{
    [JsonPropertyName("userMessage")]
    public required ContentRequestItem[] UserMessage { get; init; }

    [JsonPropertyName("parentAssistantMessageId")]
    public required string? ParentAssistantMessageId { get; init; }

    public override WebChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new GeneralChatRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            UserMessage = UserMessage,
            ParentAssistantMessageId = urlEncryption.DecryptTurnIdOrEmpty(ParentAssistantMessageId)
        };
    }
}

public abstract record WebChatRequest
{
    public required int ChatId { get; init; }

    public required short TimezoneOffset { get; init; }

    public abstract long? LastMessageId { get; }
}

public record RegenerateAssistantMessageRequest : RegenerateAllAssistantMessageRequest
{
    public required byte SpanId { get; init; }
}

public record RegenerateAllAssistantMessageRequest : WebChatRequest
{
    public required long ParentUserMessageId { get; init; }

    public required short ModelId { get; init; }

    public override long? LastMessageId => ParentUserMessageId;
}

public record GeneralChatRequest : WebChatRequest
{
    public required ContentRequestItem[] UserMessage { get; init; }

    public required long? ParentAssistantMessageId { get; init; }

    public override long? LastMessageId => ParentAssistantMessageId;
}