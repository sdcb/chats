using Chats.BE.DB;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(RequestMessageDto))]
[JsonDerivedType(typeof(ResponseMessageDto))]
public abstract record MessageDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("parentId")]
    public required string? ParentId { get; init; }

    [JsonPropertyName("role")]
    public required DBChatRole Role { get; init; }

    [JsonPropertyName("content")]
    public required ContentResponseItem[] Content { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("spanId")]
    public required byte? SpanId { get; init; }

    [JsonPropertyName("edited")]
    public required bool Edited { get; init; }
}

public record RequestMessageDto : MessageDto
{
    public static RequestMessageDto FromDB(Message message, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return new RequestMessageDto()
        {
            Id = urlEncryption.EncryptMessageId(message.Id),
            ParentId = urlEncryption.EncryptMessageId(message.ParentId),
            Role = (DBChatRole)message.ChatRoleId,
            Content = ContentResponseItem.FromSegment([.. message.MessageContents], fup, urlEncryption),
            CreatedAt = message.CreatedAt,
            SpanId = message.SpanId,
            Edited = message.Edited,
        };
    }
}

public record ResponseMessageDto : MessageDto
{
    [JsonPropertyName("inputTokens")]
    public required int InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public required int OutputTokens { get; init; }

    [JsonPropertyName("inputPrice")]
    public required decimal InputPrice { get; init; }

    [JsonPropertyName("outputPrice")]
    public required decimal OutputPrice { get; init; }

    [JsonPropertyName("reasoningTokens")]
    public required int ReasoningTokens { get; init; }

    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    [JsonPropertyName("reasoningDuration")]
    public required int ReasoningDuration { get; init; }

    [JsonPropertyName("firstTokenLatency")]
    public required int FirstTokenLatency { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    [JsonPropertyName("modelName")]
    public required string? ModelName { get; init; }

    [JsonPropertyName("modelProviderId")]
    public required short ModelProviderId { get; init; }

    [JsonPropertyName("reaction")]
    public required bool? Reaction { get; init; }
}

public record FileDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }
}

public record ChatMessageTempUsage
{
    public required int Duration { get; init; }
    public required int ReasoningDuration { get; init; }
    public required int FirstTokenLatency { get; init; }
    public required decimal InputPrice { get; init; }
    public required int InputTokens { get; init; }
    public required short ModelId { get; init; }
    public required string ModelName { get; init; }
    public required bool? Reaction { get; init; }

    public required short ModelProviderId { get; init; }
    public required decimal OutputPrice { get; init; }
    public required int OutputTokens { get; init; }
    public required int ReasoningTokens { get; init; }
}

public record ChatMessageTemp
{
    public required long Id { get; init; }
    public required long? ParentId { get; init; }
    public required DBChatRole Role { get; init; }
    public required MessageContent[] Content { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required byte? SpanId { get; init; }
    public required bool Edited { get; init; }
    public required ChatMessageTempUsage? Usage { get; init; }

    public MessageDto ToDto(IUrlEncryptionService urlEncryption, FileUrlProvider fup)
    {
        if (Usage == null)
        {
            return new RequestMessageDto()
            {
                Id = urlEncryption.EncryptMessageId(Id),
                ParentId = ParentId != null ? urlEncryption.EncryptMessageId(ParentId.Value) : null, 
                Role = Role,
                Content = ContentResponseItem.FromSegment(Content, fup, urlEncryption),
                CreatedAt = CreatedAt,
                SpanId = SpanId,
                Edited = Edited,
            };
        }
        else
        {
            return new ResponseMessageDto()
            {
                Id = urlEncryption.EncryptMessageId(Id),
                ParentId = ParentId != null ? urlEncryption.EncryptMessageId(ParentId.Value) : null, 
                Role = Role,
                Content = ContentResponseItem.FromSegment(Content, fup, urlEncryption),
                CreatedAt = CreatedAt,
                SpanId = SpanId,
                Edited = Edited,

                InputTokens = Usage.InputTokens,
                OutputTokens = Usage.OutputTokens,
                InputPrice = Usage.InputPrice,
                OutputPrice = Usage.OutputPrice,
                ReasoningTokens = Usage.ReasoningTokens,
                Duration = Usage.Duration,
                ReasoningDuration = Usage.ReasoningDuration,
                FirstTokenLatency = Usage.FirstTokenLatency,
                ModelId = Usage.ModelId,
                ModelName = Usage.ModelName,
                ModelProviderId = Usage.ModelProviderId,
                Reaction = Usage.Reaction,
            };
        }
    }

    public static ChatMessageTemp FromDB(Message assistantMessage)
    {
        if (assistantMessage.ChatRoleId == (byte)DBChatRole.Assistant)
        {
            if (assistantMessage.MessageResponse?.Usage == null) throw new InvalidOperationException("Assistant message must have usage data");

            return new()
            {
                Content = [.. assistantMessage.MessageContents],
                CreatedAt = assistantMessage.CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = (DBChatRole)assistantMessage.ChatRoleId,
                SpanId = assistantMessage.SpanId,
                Edited = assistantMessage.Edited,
                Usage = new ChatMessageTempUsage()
                {
                    Duration = assistantMessage.MessageResponse.Usage.TotalDurationMs - assistantMessage.MessageResponse.Usage.PreprocessDurationMs,
                    ReasoningDuration = assistantMessage.MessageResponse.Usage.ReasoningDurationMs,
                    FirstTokenLatency = assistantMessage.MessageResponse.Usage.FirstResponseDurationMs,
                    InputPrice = assistantMessage.MessageResponse.Usage.InputCost,
                    InputTokens = assistantMessage.MessageResponse.Usage.InputTokens,
                    ModelId = assistantMessage.MessageResponse.Usage.UserModel.ModelId,
                    ModelName = assistantMessage.MessageResponse.Usage.UserModel.Model.Name,
                    OutputPrice = assistantMessage.MessageResponse.Usage.OutputCost,
                    OutputTokens = assistantMessage.MessageResponse.Usage.OutputTokens,
                    ReasoningTokens = assistantMessage.MessageResponse.Usage.ReasoningTokens,
                    ModelProviderId = assistantMessage.MessageResponse.Usage.UserModel.Model.ModelKey.ModelProviderId,
                    Reaction = assistantMessage.MessageResponse.ReactionId,
                },
            };
        }
        else
        {
            // user/system message
            return new()
            {
                Content = [.. assistantMessage.MessageContents],
                CreatedAt = assistantMessage.CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = (DBChatRole)assistantMessage.ChatRoleId,
                SpanId = assistantMessage.SpanId,
                Edited = assistantMessage.Edited,
                Usage = null,
            };
        }
    }
}