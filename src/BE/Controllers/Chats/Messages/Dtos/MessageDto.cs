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
    public static RequestMessageDto FromDB(ChatTurn message, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return new RequestMessageDto()
        {
            Id = urlEncryption.EncryptMessageId(message.Id),
            ParentId = urlEncryption.EncryptMessageId(message.ParentId),
            Role = message.IsUser ? DBChatRole.User : DBChatRole.Assistant,
            Content = ContentResponseItem.FromContent([.. message.Steps.SelectMany(x => x.StepContents)], fup, urlEncryption),
            CreatedAt = message.Steps.First().CreatedAt,
            SpanId = message.SpanId,
            Edited = message.Steps.Any(x => x.Edited),
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

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
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
    public required StepContent[] Content { get; init; }
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
                Content = ContentResponseItem.FromContent(Content, fup, urlEncryption),
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
                Content = ContentResponseItem.FromContent(Content, fup, urlEncryption),
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

    public static ChatMessageTemp FromDB(ChatTurn assistantMessage)
    {
        if (assistantMessage.IsUser)
        {
            // user/system message
            return new()
            {
                Content = [.. assistantMessage.Steps.SelectMany(x => x.StepContents)],
                CreatedAt = assistantMessage.Steps.First().CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = DBChatRole.User,
                SpanId = assistantMessage.SpanId,
                Edited = false,
                Usage = null,
            };
        }
        else
        {
            if (assistantMessage.Steps.All(x => x.Usage == null)) throw new InvalidOperationException("Assistant message must have usage data");

            return new()
            {
                Content = [.. assistantMessage.Steps.SelectMany(x => x.StepContents)],
                CreatedAt = assistantMessage.Steps.First().CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = DBChatRole.Assistant,
                SpanId = assistantMessage.SpanId,
                Edited = assistantMessage.Steps.Any(x => x.Edited),
                Usage = new ChatMessageTempUsage()
                {
                    InputTokens = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.InputTokens),
                    OutputTokens = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.OutputTokens),
                    InputPrice = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.InputCost),
                    OutputPrice = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.OutputCost),
                    ReasoningTokens = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.ReasoningTokens),
                    Duration = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.TotalDurationMs),
                    ReasoningDuration = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.ReasoningDurationMs),
                    FirstTokenLatency = assistantMessage.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.FirstResponseDurationMs),
                    ModelId = assistantMessage.Steps.First().Usage!.ModelId,
                    ModelName = assistantMessage.Steps.First().Usage!.Model.Name,
                    ModelProviderId = assistantMessage.Steps.First().Usage!.Model.ModelKey.ModelProviderId,
                    Reaction = assistantMessage.ReactionId,
                },
            };
        }
    }
}