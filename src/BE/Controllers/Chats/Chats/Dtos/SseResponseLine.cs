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
    public static ResponseTurnLine ResponseMessage(
        byte spanId,
        ChatTurn assistantTurn,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp assistantTemp = ChatMessageTemp.FromDB(assistantTurn);
        TurnDto dto = assistantTemp.ToDto(urlEncryptionService, fup);
        return new(spanId, dto);
    }

    public static UserTurnLine UserTurn(
        ChatTurn userTurn,
        IUrlEncryptionService urlEncryptionService,
        FileUrlProvider fup)
    {
        ChatMessageTemp userTemp = ChatMessageTemp.FromDB(userTurn);
        TurnDto dto = userTemp.ToDto(urlEncryptionService, fup);
        return new(dto);
    }

    public static ChatLeafTurnIdLine ChatLeafTurnId(
        long leafMessageId,
        IUrlEncryptionService idEncryption) =>
        new(idEncryption.EncryptTurnId(leafMessageId));
}

#region 派生 record

public sealed record SegmentLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] string Segment
) : SseResponseLine;

public sealed record ReasoningSegmentLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] string Segment
) : SseResponseLine;

public sealed record StartResponseLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] int ReasoningTimeMs
) : SseResponseLine;

public sealed record StartReasoningLine(
    [property: JsonPropertyName("i")] byte SpanId
) : SseResponseLine;

public sealed record ErrorLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] string Error
) : SseResponseLine;

public sealed record ResponseTurnLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] TurnDto Message
) : SseResponseLine;

public sealed record UserTurnLine(
    [property: JsonPropertyName("r")] TurnDto Message
) : SseResponseLine;

public sealed record StopIdLine(
    [property: JsonPropertyName("r")] string StopId
) : SseResponseLine;

public sealed record UpdateTitleLine(
    [property: JsonPropertyName("r")] string Title
) : SseResponseLine;

public sealed record TitleSegmentLine(
    [property: JsonPropertyName("r")] string TitleSegment
) : SseResponseLine;

public sealed record ChatLeafTurnIdLine(
    [property: JsonPropertyName("r")] string EncryptedLeafMessageId
) : SseResponseLine;

public sealed record ImageGeneratingLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] FileDto File
) : SseResponseLine;

public sealed record TempImageGeneratedLine(
    byte SpanId,
    ImageChatSegment Image
) : SseResponseLine;

public sealed record ImageGeneratedLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] FileDto File
) : SseResponseLine;

public sealed record CallingToolLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("u")] string ToolCallId,
    [property: JsonPropertyName("r")] string ToolName,
    [property: JsonPropertyName("p")] string Parameters
) : SseResponseLine;

public sealed record ToolProgressLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("u")] string ToolCallId,
    [property: JsonPropertyName("r")] string Progress
) : SseResponseLine;

public sealed record ToolCompletedLine(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("s")] bool Success,
    [property: JsonPropertyName("u")] string ToolCallId,
    [property: JsonPropertyName("r")] string Result
) : SseResponseLine;

public sealed record EndStep(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] StepDto Step
) : SseResponseLine;

public sealed record EndStepInternal(
    byte SpanId,
    Step Step
) : SseResponseLine;

public sealed record EndTurn(
    [property: JsonPropertyName("i")] byte SpanId,
    [property: JsonPropertyName("r")] ChatTurn Turn
) : SseResponseLine;

#endregion