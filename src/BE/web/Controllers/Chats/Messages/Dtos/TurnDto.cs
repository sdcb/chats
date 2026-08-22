using Chats.BE.Services.FileServices;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(RequestMessageDto))]
[JsonDerivedType(typeof(ResponseMessageDto))]
public abstract record TurnDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("parentId")]
    public required string? ParentId { get; init; }

    [JsonPropertyName("role")]
    public required DBChatRole Role { get; init; }

    [JsonPropertyName("steps")]
    public required StepDto[] Steps { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("spanId")]
    public required byte? SpanId { get; init; }

    [JsonPropertyName("siblingIds")]
    public string[] SiblingIds { get; init; } = [];
}

public record RequestMessageDto : TurnDto
{
    public static RequestMessageDto FromDB(ChatTurn message, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return new RequestMessageDto()
        {
            Id = urlEncryption.EncryptTurnId(message.Id),
            ParentId = urlEncryption.EncryptTurnId(message.ParentId),
            Role = message.IsUser ? DBChatRole.User : DBChatRole.Assistant,
            Steps = StepDto.FromDB(message.Steps, fup, urlEncryption),
            CreatedAt = message.Steps.First().CreatedAt,
            SpanId = message.SpanId,
        };
    }
}

public record ResponseMessageDto : TurnDto
{
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
    [IgnoreForEtagHash]
    public string? Url { get; init; }
}

public record ChatMessageTempUsage
{
    public required short ModelId { get; init; }
    public required string ModelName { get; init; }
    public required short ModelProviderId { get; init; }
}

public record ChatMessageTemp
{
    public required long Id { get; init; }
    public required long? ParentId { get; init; }
    public required DBChatRole Role { get; init; }
    public required Step[] Steps { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required byte? SpanId { get; init; }
    public required ChatMessageTempUsage? Usage { get; init; }
    public required bool? Reaction { get; init; }

    public TurnDto ToDto(IUrlEncryptionService urlEncryption, FileUrlProvider fup, IReadOnlyList<long>? siblingIds = null)
    {
        if (Role == DBChatRole.User)
        {
            return new RequestMessageDto()
            {
                Id = urlEncryption.EncryptTurnId(Id),
                ParentId = ParentId != null ? urlEncryption.EncryptTurnId(ParentId.Value) : null, 
                Role = Role,
                Steps = StepDto.FromDB(Steps, fup, urlEncryption),
                CreatedAt = CreatedAt,
                SpanId = SpanId,
                SiblingIds = EncryptSiblingIds(siblingIds, urlEncryption),
            };
        }
        else
        {
            return new ResponseMessageDto()
            {
                Id = urlEncryption.EncryptTurnId(Id),
                ParentId = ParentId != null ? urlEncryption.EncryptTurnId(ParentId.Value) : null, 
                Role = Role,
                Steps = StepDto.FromDB(Steps, fup, urlEncryption),
                CreatedAt = CreatedAt,
                SpanId = SpanId,

                ModelId = Usage?.ModelId ?? 0,
                ModelName = Usage?.ModelName,
                ModelProviderId = Usage?.ModelProviderId ?? 0,
                Reaction = Reaction,
                SiblingIds = EncryptSiblingIds(siblingIds, urlEncryption),
            };
        }
    }

    private static string[] EncryptSiblingIds(IReadOnlyList<long>? siblingIds, IUrlEncryptionService urlEncryption) =>
        siblingIds == null ? [] : [.. siblingIds.Select(urlEncryption.EncryptTurnId)];

    public static ChatMessageTemp FromDB(ChatTurn assistantMessage)
    {
        if (assistantMessage.IsUser)
        {
            // user/system message
            return new()
            {
                Steps = [.. assistantMessage.Steps.OrderBy(x => x.Id)],
                CreatedAt = assistantMessage.Steps.First().CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = DBChatRole.User,
                SpanId = assistantMessage.SpanId,
                Usage = null,
                Reaction = null,
            };
        }
        else
        {
            ModelSnapshot? usageModelSnapshot = assistantMessage.Steps
                .Where(x => x.Usage != null)
                .Select(x => x.Usage!)
                .FirstOrDefault()?
                .ModelSnapshot;
            ModelSnapshot? chatConfigModelSnapshot = assistantMessage.ChatConfigSnapshot?.ModelSnapshot;
            ModelSnapshot? resolvedModelSnapshot = usageModelSnapshot ?? chatConfigModelSnapshot;

            return new()
            {
                Steps = [.. assistantMessage.Steps.OrderBy(x => x.Id)],
                CreatedAt = assistantMessage.Steps.First().CreatedAt,
                Id = assistantMessage.Id,
                ParentId = assistantMessage.ParentId,
                Role = DBChatRole.Assistant,
                SpanId = assistantMessage.SpanId,
                Usage = resolvedModelSnapshot == null ? null : new ChatMessageTempUsage()
                {
                    ModelId = resolvedModelSnapshot?.ModelId ?? 0,
                    ModelName = resolvedModelSnapshot?.Name ?? string.Empty,
                    ModelProviderId = resolvedModelSnapshot?.ModelKeySnapshot.ModelProviderId ?? 0,
                },
                Reaction = assistantMessage.ReactionId,
            };
        }
    }
}
