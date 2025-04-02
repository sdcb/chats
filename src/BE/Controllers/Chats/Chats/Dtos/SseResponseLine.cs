using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record SseResponseLine
{
    [JsonPropertyName("k")]
    public required SseResponseKind Kind { get; init; }

    [JsonPropertyName("i"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? SpanId { get; init; }

    [JsonPropertyName("r")]
    public required object Result { get; init; }

    public static SseResponseLine Segment(byte spanId, string segment)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = segment,
            Kind = SseResponseKind.Segment,
        };
    }

    public static SseResponseLine ReasoningSegment(byte spanId, string segment)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = segment,
            Kind = SseResponseKind.ReasoningSegment,
        };
    }

    public static SseResponseLine StartResponse(byte spanId, int reasoningTimeMs)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = reasoningTimeMs,
            Kind = SseResponseKind.StartResponse,
        };
    }

    public static SseResponseLine StartReasoning(byte spanId)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = null!,
            Kind = SseResponseKind.StartReasoning,
        };
    }

    public static SseResponseLine Error(byte spanId, string error)
    {
        return new SseResponseLine
        {
            Result = error,
            SpanId = spanId,
            Kind = SseResponseKind.Error,
        };
    }

    public static SseResponseLine ResponseMessage(
        byte spanId,
        Message assistantMessage,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp assistantMessageTemp = ChatMessageTemp.FromDB(assistantMessage);
        MessageDto assistantMessageDto = assistantMessageTemp.ToDto(urlEncryptionService, fup);
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = assistantMessageDto,
            Kind = SseResponseKind.ResponseMessage,
        };
    }

    public static SseResponseLine UserMessage(
        Message userMessage,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp userMessageTemp = ChatMessageTemp.FromDB(userMessage);
        MessageDto userMessageDto = userMessageTemp.ToDto(urlEncryptionService, fup);
        return new SseResponseLine
        {
            Result = userMessageDto,
            Kind = SseResponseKind.UserMessage,
        };
    }

    public static SseResponseLine StopId(string stopId)
    {
        return new SseResponseLine
        {
            Result = stopId,
            Kind = SseResponseKind.StopId,
        };
    }

    public static SseResponseLine UpdateTitle(string title)
    {
        return new SseResponseLine
        {
            Result = title,
            Kind = SseResponseKind.UpdateTitle,
        };
    }

    public static SseResponseLine TitleSegment(string titleSegment)
    {
        return new SseResponseLine
        {
            Result = titleSegment,
            Kind = SseResponseKind.TitleSegment,
        };
    }

    public static SseResponseLine ChatLeafMessageId(long leafMessageId, IUrlEncryptionService idEncryption)
    {
        return new SseResponseLine
        {
            Result = idEncryption.EncryptMessageId(leafMessageId),
            Kind = SseResponseKind.ChatLeafMessageId,
        };
    }

    public static SseResponseLine ImageGenerating(byte spanId, FileDto fileDto)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = fileDto,
            Kind = SseResponseKind.ImageGenerating,
        };
    }

    public static SseResponseLine ImageGenerated(byte spanId, FileDto fileDto)
    {
        return new SseResponseLine
        {
            SpanId = spanId,
            Result = fileDto,
            Kind = SseResponseKind.ImageGenerated,
        };
    }
}
