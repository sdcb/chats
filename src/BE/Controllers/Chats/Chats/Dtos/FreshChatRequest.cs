using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public abstract record EncryptedChatRequest
{
    [JsonPropertyName("chatId")]
    public required string EncryptedChatId { get; init; }

    [JsonPropertyName("timezoneOffset")]
    public required short TimezoneOffset { get; init; }

    public abstract ChatRequest Decrypt(IUrlEncryptionService urlEncryption);
}

public record EncryptedRegenerateAssistantMessageRequest : EncryptedChatRequest
{
    [JsonPropertyName("parentUserMessageId")]
    public required string ParentUserMessageId { get; init; }

    [JsonPropertyName("spanId")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    public override ChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new RegenerateAssistantMessageRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            ParentUserMessageId = urlEncryption.DecryptMessageId(ParentUserMessageId),
            SpanId = SpanId,
            ModelId = ModelId
        };
    }
}

public record EncryptedRegenerateAllAssistantMessageRequest : EncryptedChatRequest
{
    [JsonPropertyName("parentUserMessageId")]
    public required string ParentUserMessageId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    public override ChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new RegenerateAllAssistantMessageRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            ParentUserMessageId = urlEncryption.DecryptMessageId(ParentUserMessageId),
            ModelId = ModelId
        };
    }
}

public record EncryptedGeneralChatRequest : EncryptedChatRequest
{
    [JsonPropertyName("userMessage")]
    public required ContentRequestItem[] UserMessage { get; init; }

    [JsonPropertyName("parentAssistantMessageId")]
    public required string? ParentAssistantMessageId { get; init; }

    public override ChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new GeneralChatRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            UserMessage = UserMessage,
            ParentAssistantMessageId = urlEncryption.DecryptMessageIdOrNull(ParentAssistantMessageId)
        };
    }
}

public abstract record ChatRequest
{
    public required int ChatId { get; init; }

    public required short TimezoneOffset { get; init; }

    public abstract long? LastMessageId { get; }
}

public record RegenerateAssistantMessageRequest : RegenerateAllAssistantMessageRequest
{
    public required byte SpanId { get; init; }
}

public record RegenerateAllAssistantMessageRequest : ChatRequest
{
    public required long ParentUserMessageId { get; init; }

    public required short ModelId { get; init; }

    public override long? LastMessageId => ParentUserMessageId;
}

public record GeneralChatRequest : ChatRequest
{
    public required ContentRequestItem[] UserMessage { get; init; }

    public required long? ParentAssistantMessageId { get; init; }

    public override long? LastMessageId => ParentAssistantMessageId;
}