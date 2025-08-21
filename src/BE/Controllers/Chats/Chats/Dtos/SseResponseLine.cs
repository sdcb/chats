using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

/// <summary>
/// SSE 一行的抽象基类，实际序列化时由 k 字段区分不同派生类型。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "k")]
[JsonDerivedType(typeof(SegmentLine          ), (int)SseResponseKind.Segment          )]
[JsonDerivedType(typeof(ReasoningSegmentLine ), (int)SseResponseKind.ReasoningSegment )]
[JsonDerivedType(typeof(StartResponseLine    ), (int)SseResponseKind.StartResponse    )]
[JsonDerivedType(typeof(StartReasoningLine   ), (int)SseResponseKind.StartReasoning   )]
[JsonDerivedType(typeof(ErrorLine            ), (int)SseResponseKind.Error            )]
[JsonDerivedType(typeof(ResponseTurnLine     ), (int)SseResponseKind.ResponseTurn     )]
[JsonDerivedType(typeof(UserTurnLine         ), (int)SseResponseKind.UserTurn         )]
[JsonDerivedType(typeof(StopIdLine           ), (int)SseResponseKind.StopId           )]
[JsonDerivedType(typeof(UpdateTitleLine      ), (int)SseResponseKind.UpdateTitle      )]
[JsonDerivedType(typeof(TitleSegmentLine     ), (int)SseResponseKind.TitleSegment     )]
[JsonDerivedType(typeof(ChatLeafTurnIdLine   ), (int)SseResponseKind.ChatLeafTurnId   )]
[JsonDerivedType(typeof(ImageGeneratingLine  ), (int)SseResponseKind.ImageGenerating  )]
[JsonDerivedType(typeof(ImageGeneratedLine   ), (int)SseResponseKind.ImageGenerated   )]
[JsonDerivedType(typeof(CallingToolLine      ), (int)SseResponseKind.CallingTool      )]
[JsonDerivedType(typeof(ToolProgressLine     ), (int)SseResponseKind.ToolProgress     )]
[JsonDerivedType(typeof(ToolCompletedLine    ), (int)SseResponseKind.ToolCompleted    )]
[JsonDerivedType(typeof(EndStep              ), (int)SseResponseKind.EndStep          )]
[JsonDerivedType(typeof(EndTurn              ), (int)SseResponseKind.EndTurn          )]
public abstract record SseResponseLine
{
    #region 工厂方法

    public static SegmentLine CreateSegment(byte spanId, string segment) =>
        new() { SpanId = spanId, Segment = segment };

    public static ReasoningSegmentLine ReasoningSegment(byte spanId, string segment) =>
        new() { SpanId = spanId, Segment = segment };

    public static StartResponseLine StartResponse(byte spanId, int reasoningTimeMs) =>
        new() { SpanId = spanId, ReasoningTimeMs = reasoningTimeMs };

    public static StartReasoningLine StartReasoning(byte spanId) =>
        new() { SpanId = spanId };

    public static ErrorLine CreateError(byte spanId, string error) =>
        new() { SpanId = spanId, Error = error };

    public static ResponseTurnLine ResponseMessage(
        byte spanId,
        ChatTurn assistantMessage,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp assistantTemp = ChatMessageTemp.FromDB(assistantMessage);
        TurnDto dto = assistantTemp.ToDto(urlEncryptionService, fup);
        return new() { SpanId = spanId, Message = dto };
    }

    public static UserTurnLine UserMessage(
        ChatTurn userMessage,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp userTemp = ChatMessageTemp.FromDB(userMessage);
        TurnDto dto = userTemp.ToDto(urlEncryptionService, fup);
        return new() { Message = dto };
    }

    public static StopIdLine CreateStopId(string stopId) =>
        new() { StopId = stopId };

    public static UpdateTitleLine UpdateTitle(string title) =>
        new() { Title = title };

    public static TitleSegmentLine CreateTitleSegment(string titleSegment) =>
        new() { TitleSegment = titleSegment };

    public static ChatLeafTurnIdLine ChatLeafMessageId(
        long leafMessageId,
        IUrlEncryptionService idEncryption) =>
        new() { EncryptedLeafMessageId = idEncryption.EncryptTurnId(leafMessageId) };

    public static ImageGeneratingLine ImageGenerating(byte spanId, FileDto fileDto) =>
        new() { SpanId = spanId, File = fileDto };

    public static ImageGeneratedLine ImageGenerated(byte spanId, FileDto payload) =>
        new() { SpanId = spanId, File = payload };

    public static TempImageGeneratedLine TempImageGenerated(byte spanId, ImageChatSegment payload) =>
        new() { SpanId = spanId, Image = payload };

    public static CallingToolLine CallingTool(byte spanId, string toolCallId, string toolName, string parameters) =>
        new() { SpanId = spanId, ToolCallId = toolCallId, ToolName = toolName, Parameters = parameters };

    public static ToolProgressLine ToolProgress(byte spanId, string toolCallId, string progress) =>
        new() { SpanId = spanId, ToolCallId = toolCallId, Progress = progress };

    public static ToolCompletedLine ToolEnd(byte spanId, string toolCallId, string result) =>
        new() { SpanId = spanId, ToolCallId = toolCallId, Result = result };

    public static EndStep EndStep(byte spanId, Step step) => new() { SpanId = spanId, Step = step };

    public static EndTurn EndTurn(byte spanId, ChatTurn turn) => new() { SpanId = spanId, Turn = turn };

    #endregion
}

#region 派生 record

public sealed record SegmentLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required string Segment { get; init; }
}

public sealed record ReasoningSegmentLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required string Segment { get; init; }
}

public sealed record StartResponseLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required int ReasoningTimeMs { get; init; }
}

public sealed record StartReasoningLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }
}

public sealed record ErrorLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required string Error { get; init; }
}

public sealed record ResponseTurnLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required TurnDto Message { get; init; }
}

public sealed record UserTurnLine : SseResponseLine
{
    [JsonPropertyName("r")]
    public required TurnDto Message { get; init; }
}

public sealed record StopIdLine : SseResponseLine
{
    [JsonPropertyName("r")]
    public required string StopId { get; init; }
}

public sealed record UpdateTitleLine : SseResponseLine
{
    [JsonPropertyName("r")]
    public required string Title { get; init; }
}

public sealed record TitleSegmentLine : SseResponseLine
{
    [JsonPropertyName("r")]
    public required string TitleSegment { get; init; }
}

public sealed record ChatLeafTurnIdLine : SseResponseLine
{
    [JsonPropertyName("r")]
    public required string EncryptedLeafMessageId { get; init; }
}

public sealed record ImageGeneratingLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required FileDto File { get; init; }
}

public sealed record TempImageGeneratedLine : SseResponseLine
{
    public required byte SpanId { get; init; }

    public required ImageChatSegment Image { get; init; }
}

public sealed record ImageGeneratedLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required FileDto File { get; init; }
}

public sealed record CallingToolLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }
    [JsonPropertyName("u")]
    public required string ToolCallId { get; init; }
    [JsonPropertyName("r")]
    public required string ToolName { get; init; }
    [JsonPropertyName("p")]
    public required string Parameters { get; init; }
}

public sealed record ToolProgressLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }
    [JsonPropertyName("u")]
    public required string ToolCallId { get; init; }
    [JsonPropertyName("r")]
    public required string Progress { get; init; }
}

public sealed record ToolCompletedLine : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }
    [JsonPropertyName("u")]
    public required string ToolCallId { get; init; }
    [JsonPropertyName("r")]
    public required string Result { get; init; }
}

public sealed record EndStep : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required Step Step { get; init; }
}

public sealed record EndTurn : SseResponseLine
{
    [JsonPropertyName("i")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("r")]
    public required ChatTurn Turn { get; init; }
}

#endregion