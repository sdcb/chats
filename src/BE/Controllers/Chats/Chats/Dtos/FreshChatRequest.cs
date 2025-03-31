using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public abstract record ChatRequest
{
    [JsonPropertyName("chatId")]
    public required string EncryptedChatId { get; init; }

    [JsonPropertyName("timezoneOffset")]
    public required short TimezoneOffset { get; init; }

    public abstract DecryptedChatRequest Decrypt(IUrlEncryptionService urlEncryption);
}

public record RegenerateAssistantMessageRequest : ChatRequest
{
    [JsonPropertyName("parentUserMessageId")]
    public required string ParentUserMessageId { get; init; }

    [JsonPropertyName("spanId")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    public override DecryptedChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new DecryptedRegenerateAssistantMessageRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            ParentUserMessageId = urlEncryption.DecryptMessageId(ParentUserMessageId),
            SpanId = SpanId,
            ModelId = ModelId
        };
    }
}

public record GeneralChatRequest : ChatRequest
{
    [JsonPropertyName("userMessage")]
    public required ContentRequestItem[] UserMessage { get; init; }

    [JsonPropertyName("parentAssistantMessageId")]
    public required string? ParentAssistantMessageId { get; init; }

    public override DecryptedChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new DecryptedGeneralChatRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            UserMessage = UserMessage,
            ParentAssistantMessageId = urlEncryption.DecryptMessageIdOrNull(ParentAssistantMessageId)
        };
    }
}

public abstract record DecryptedChatRequest
{
    public required int ChatId { get; init; }

    public required short TimezoneOffset { get; init; }

    public abstract long? LastMessageId { get; }
}

public record DecryptedRegenerateAssistantMessageRequest : DecryptedChatRequest
{
    public required long ParentUserMessageId { get; init; }

    public required byte SpanId { get; init; }

    public required short ModelId { get; init; }

    public override long? LastMessageId => ParentUserMessageId;
}

public record DecryptedGeneralChatRequest : DecryptedChatRequest
{
    public required ContentRequestItem[] UserMessage { get; init; }

    public required long? ParentAssistantMessageId { get; init; }

    public override long? LastMessageId => ParentAssistantMessageId;
}