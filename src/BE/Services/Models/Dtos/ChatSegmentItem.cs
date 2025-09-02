using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Chats.BE.Services.Models.ChatServices;
using Mscc.GenerativeAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.Models.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(Base64Image), typeDiscriminator: "base64")]
[JsonDerivedType(typeof(UrlImage), typeDiscriminator: "url")]
[JsonDerivedType(typeof(ToolCallSegment), typeDiscriminator: "toolcall")]
[JsonDerivedType(typeof(TextChatSegment), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ThinkChatSegment), typeDiscriminator: "think")]
public abstract record ChatSegmentItem
{
    public static ChatSegmentItem FromText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        return new TextChatSegment { Text = text };
    }

    public static ChatSegmentItem FromThink(string think)
    {
        ArgumentException.ThrowIfNullOrEmpty(think, nameof(think));
        return new ThinkChatSegment { Think = think };
    }

    public static ImageChatSegment FromBase64Image(string base64, string contentType)
    {
        return new Base64Image
        {
            Base64 = base64,
            ContentType = contentType
        };
    }

    public static ImageChatSegment FromBinaryData(BinaryData binaryData, string contentType)
    {
        return new Base64Image
        {
            Base64 = Convert.ToBase64String(binaryData.ToArray()),
            ContentType = contentType
        };
    }

    public static ImageChatSegment FromUrlImage(string url)
    {
        return new UrlImage
        {
            Url = url
        };
    }

    public static List<ChatSegmentItem> FromTextAndThink(string? text, string? think)
    {
        List<ChatSegmentItem> segments = new(capacity: 2);
        if (!string.IsNullOrEmpty(text))
        {
            segments.Add(FromText(text));
        }
        if (!string.IsNullOrEmpty(think))
        {
            segments.Add(FromThink(think));
        }
        return segments;
    }

    public static List<ChatSegmentItem> FromTextThinkToolCall(string? text, string? think, IReadOnlyList<StreamingChatToolCallUpdate>? toolCalls)
    {
        List<ChatSegmentItem> segments = new(capacity: 2);
        if (!string.IsNullOrEmpty(text))
        {
            segments.Add(FromText(text));
        }
        if (!string.IsNullOrEmpty(think))
        {
            segments.Add(FromThink(think));
        }
        if (toolCalls != null && toolCalls.Count != 0)
        {
            foreach (StreamingChatToolCallUpdate toolCall in toolCalls)
            {
                segments.Add(FromToolCall(toolCall));
            }
        }
        return segments;
    }

    public static List<ChatSegmentItem> FromTextThinkToolCall(string? text, string? think, IReadOnlyList<ChatToolCall>? toolCalls)
    {
        List<ChatSegmentItem> segments = new(capacity: 2);
        if (!string.IsNullOrEmpty(text))
        {
            segments.Add(FromText(text));
        }
        if (!string.IsNullOrEmpty(think))
        {
            segments.Add(FromThink(think));
        }
        if (toolCalls != null && toolCalls.Count != 0)
        {
            segments.AddRange(FromToolCalls(toolCalls));
        }
        return segments;
    }

    public static ToolCallSegment FromToolCall(StreamingResponseOutputItemAddedUpdate delta, FunctionCallResponseItem toolCall)
    {
        return new ToolCallSegment
        {
            Index = delta.OutputIndex,
            Id = toolCall.Id,
            Type = ChatToolCallKind.Function.ToString(),
            Name = toolCall.FunctionName,
            Arguments = GetBinaryData(toolCall.FunctionArguments).Length == 0 ? "" : toolCall.FunctionArguments.ToString(),
        };
    }

    public static ToolCallSegment FromToolCall(int fcIndex, FunctionCallResponseItem toolCall)
    {
        return new ToolCallSegment
        {
            Index = fcIndex,
            Id = toolCall.Id,
            Type = ChatToolCallKind.Function.ToString(),
            Name = toolCall.FunctionName,
            Arguments = GetBinaryData(toolCall.FunctionArguments).Length == 0 ? "" : toolCall.FunctionArguments.ToString(),
        };
    }

    public static ToolCallSegment FromToolCall(int fcIndex, FunctionCall toolCall)
    {
        return new ToolCallSegment
        {
            Index = fcIndex,
            Id = toolCall.Id ?? fcIndex.ToString(),
            Type = ChatToolCallKind.Function.ToString(),
            Name = toolCall.Name,
            Arguments = JSON.Serialize(toolCall.Args)
        };
    }

    public static ToolCallSegment FromToolCallDelta(StreamingResponseFunctionCallArgumentsDeltaUpdate delta)
    {
        return new ToolCallSegment
        {
            Index = delta.OutputIndex,
            Id = delta.ItemId,
            Type = ChatToolCallKind.Function.ToString(),
            Name = null,
            Arguments = delta.Delta,
        };
    }

    static ToolCallSegment FromToolCall(StreamingChatToolCallUpdate toolCall)
    {
        return new ToolCallSegment
        {
            Index = toolCall.Index,
            Id = toolCall.ToolCallId switch
            {
                null or "" => null,
                _ => toolCall.ToolCallId
            },
            Type = toolCall.Kind.ToString(),
            Name = toolCall.FunctionName,
            Arguments = GetBinaryData(toolCall.FunctionArgumentsUpdate).Length == 0 ? "" : toolCall.FunctionArgumentsUpdate.ToString(),
        };
    }

    static IEnumerable<ChatSegmentItem> FromToolCalls(IReadOnlyList<ChatToolCall> toolCall)
    {
        return toolCall.Select((x, i) => new ToolCallSegment()
        {
            Index = i,
            Id = x.Id,
            Type = x.Kind.ToString(),
            Name = x.FunctionName,
            Arguments = GetBinaryData(x.FunctionArguments).Length == 0 ? "" : x.FunctionArguments.ToString(),
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bytes")]
    static extern ref ReadOnlyMemory<byte> GetBinaryData(BinaryData binaryData);

    public ChatCompletionChunk ToOpenAIChatCompletionChunk(string modelName, string traceId, string? systemFingerprint)
    {
        return new ChatCompletionChunk()
        {
            Id = traceId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            Choices =
            [
                new DeltaChoice
                {
                    Index = 0,
                    Delta = new ChatSegmentItem[]{ this }.ToOpenAIDelta(),
                    FinishReason = null,
                    Logprobs = null,
                }
            ],
            SystemFingerprint = systemFingerprint,
            Usage = null,
        };
    }
}

public record TextChatSegment : ChatSegmentItem
{
    public required string Text { get; init; }
}

public record ThinkChatSegment : ChatSegmentItem
{
    public required string Think { get; init; }
}

public static class ChatSegmentItemExtensions
{
    public static OpenAIDelta ToOpenAIDelta(this ICollection<ChatSegmentItem> items)
    {
        return new OpenAIDelta
        {
            Content = GetText(items),
            ReasoningContent = GetThink(items),
            Image = items.OfType<ImageChatSegment>().FirstOrDefault(),
            ToolCalls = items.OfType<ToolCallSegment>().Select(x => new OpenAIToolCallSegment
            {
                Id = x.Id,
                Type = x.Type,
                Function = new OpenAIToolCallSegmentFunction()
                {
                    Arguments = x.Arguments,
                    Name = x.Name,
                },
                Index = x.Index,
            }).ToArray() switch { { Length: 0 } => null, var x => x }
        };
    }

    public static void AddOne(this List<ChatSegmentItem> items,
                              ICollection<ChatSegmentItem> toAddItems)
    {
        foreach (ChatSegmentItem item in toAddItems)
        {
            // 列表为空，直接放进去
            if (items.Count == 0)
            {
                items.Add(item);
                continue;
            }

            ChatSegmentItem last = items[^1];

            // ───── 文本片段合并 ──────────────────────────────────
            if (last is TextChatSegment lastText && item is TextChatSegment curText)
            {
                items[^1] = lastText with { Text = lastText.Text + curText.Text };
            }
            // ───── 思考片段合并 ─────────────────────────────────
            else if (last is ThinkChatSegment lastThink && item is ThinkChatSegment curThink)
            {
                items[^1] = lastThink with { Think = lastThink.Think + curThink.Think };
            }
            // ───── Tool‑Call 片段合并 ───────────────────────────
            else if (last is ToolCallSegment lastTool && item is ToolCallSegment curTool
                     && lastTool.Index == curTool.Index)          // 只合并同 Index
            {
                items[^1] = lastTool with
                {
                    Arguments = lastTool.Arguments + curTool.Arguments,
                    // 如果前一段缺字段，用当前段补全
                    Id = lastTool.Id ?? curTool.Id,
                    Type = lastTool.Type ?? curTool.Type,
                    Name = lastTool.Name ?? curTool.Name
                };
            }
            // ───── 其它情况：直接追加 ───────────────────────────
            else
            {
                items.Add(item);
            }
        }
    }

    public static OpenAIFullResponse OpenAIFullResponse(this ICollection<ChatSegmentItem> items, string role, object? refusal)
    {
        // items is combined
        return new OpenAIFullResponse
        {
            Role = role,
            Content = GetText(items),
            ReasoningContent = GetThink(items),
            ToolCalls = GetToolCalls(items),
            Segments = items,
            Refusal = refusal,
        };
    }

    public static string? GetText(this ICollection<ChatSegmentItem> items)
    {
        return string.Concat(items.OfType<TextChatSegment>().Select(x => x.Text)) switch { "" => null, var x => x };
    }

    public static string? GetThink(this ICollection<ChatSegmentItem> items)
    {
        return string.Concat(items.OfType<ThinkChatSegment>().Select(x => x.Think)) switch { "" => null, var x => x };
    }

    public static FullToolCall[]? GetToolCalls(this ICollection<ChatSegmentItem> items)
    {
        return ToolCall.From(items.OfType<ToolCallSegment>()).Select(x => x.ToOpenAI()).ToArray() switch { { Length: 0 } => null, var x => x };
    }

    public static ImageChatSegment[] GetImages(this ICollection<ChatSegmentItem> items)
    {
        return [.. items.OfType<ImageChatSegment>()];
    }
}