using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

[Obsolete("Use GeneralChatRequest instead")]
public record ObsoleteGeneralChatRequest : ChatRequest
{
    [JsonPropertyName("userMessage")]
    public required MessageContentRequest UserMessage { get; init; }

    [JsonPropertyName("parentAssistantMessageId")]
    public required string? ParentAssistantMessageId { get; init; }

    public override DecryptedChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new DecryptedGeneralChatRequest
        {
            ChatId = urlEncryption.DecryptChatId(EncryptedChatId),
            TimezoneOffset = TimezoneOffset,
            UserMessage = UserMessage.ToRequestItem(),
            ParentAssistantMessageId = urlEncryption.DecryptMessageIdOrNull(ParentAssistantMessageId)
        };
    }
}
