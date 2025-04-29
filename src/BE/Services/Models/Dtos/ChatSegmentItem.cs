using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Chats.BE.Services.Models.ChatServices;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.Dtos;

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

    public static ChatSegmentItem FromToolCall(StreamingChatToolCallUpdate toolCall)
    {
        return new ToolCallSegment
        {
            Index = toolCall.Index,
            Id = toolCall.ToolCallId,
            Type = toolCall.Kind.ToString(),
            Name = toolCall.FunctionName,
            Arguments = toolCall.FunctionArgumentsUpdate.ToString(),
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

    public static void AddOne(this List<ChatSegmentItem> items, ICollection<ChatSegmentItem> toAddItems)
    {
        foreach (ChatSegmentItem item in toAddItems)
        {
            if (items.Count == 0)
            {
                items.Add(item);
                continue;
            }

            // 否则尝试合并
            ChatSegmentItem last = items[^1];
            if (last is TextChatSegment lastText && item is TextChatSegment currentText)
            {
                // 两个连续 Text，合并文本
                items[^1] = lastText with { Text = lastText.Text + currentText.Text };
            }
            else if (last is ThinkChatSegment lastThink && item is ThinkChatSegment currentThink)
            {
                // 两个连续 Think，合并文本
                items[^1] = lastThink with { Think = lastThink.Think + currentThink.Think };
            }
            else
            {
                // 其他情况都不合并，直接添加
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

    public static ImageChatSegment[] GetImages(this ICollection<ChatSegmentItem> items)
    {
        return [.. items.OfType<ImageChatSegment>()];
    }
}