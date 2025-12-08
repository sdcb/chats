using Chats.BE.Controllers.Api.OpenAICompatible.Dtos;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;

namespace Chats.BE.Services.Models.Dtos;

public abstract record ChatSegment
{
    public static ChatSegment FromText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));
        return new TextChatSegment { Text = text };
    }

    public static ChatSegment FromThink(string think)
    {
        ArgumentException.ThrowIfNullOrEmpty(think, nameof(think));
        return new ThinkChatSegment { Think = think };
    }

    public static ChatSegment FromThinkingSegment(string signature)
    {
        ArgumentException.ThrowIfNullOrEmpty(signature, nameof(signature));
        return new ThinkChatSegment { Think = "", Signature = signature };
    }

    public static ImageChatSegment FromBase64Image(string base64, string contentType)
    {
        return new Base64Image
        {
            Base64 = base64,
            ContentType = contentType
        };
    }

    public static ImageChatSegment FromBase64PreviewImage(string base64, string contentType)
    {
        return new Base64PreviewImage
        {
            Base64 = base64,
            ContentType = contentType
        };
    }

    public static ImageChatSegment FromBinaryData(byte[] data, string contentType)
    {
        return new Base64Image
        {
            Base64 = Convert.ToBase64String(data),
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

    public static List<ChatSegment> FromTextAndThink(string? text, string? think)
    {
        List<ChatSegment> segments = new(capacity: 2);
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

    public static ToolCallResponseSegment FromToolCallResponse(string toolCallId, string? response, int durationMs, bool isSuccess)
    {
        return new ToolCallResponseSegment()
        {
            ToolCallId = toolCallId,
            Response = response,
            DurationMs = durationMs,
            IsSuccess = isSuccess,
        };
    }

    public static UsageChatSegment FromUsage(ChatTokenUsage usage)
        => new()
        {
            Usage = usage
        };

    public static FinishReasonChatSegment FromFinishReason(DBFinishReason? finishReason)
        => new()
        {
            FinishReason = finishReason
        };

    public ChatCompletionChunk ToOpenAIChatCompletionChunk(string modelName, string traceId, string? systemFingerprint)
    {
        bool isUsage = this is UsageChatSegment;
        bool isFinish = this is FinishReasonChatSegment;
        bool includeChoice = !isUsage;

        OpenAIDelta delta = includeChoice && !isFinish
            ? new ChatSegment[] { this }.ToOpenAIDelta()
            : new OpenAIDelta();

        string? finishReason = (this as FinishReasonChatSegment)?.FinishReason?.ToOpenAIFinishReason();
        Usage? usagePayload = (this as UsageChatSegment)?.Usage.ToOpenAIUsage();

        List<DeltaChoice> choices = includeChoice
            ? [
                new DeltaChoice
                {
                    Index = 0,
                    Delta = delta,
                    FinishReason = finishReason,
                    Logprobs = null
                }
            ]
            : [];

        return new ChatCompletionChunk()
        {
            Id = traceId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            Choices = choices,
            SystemFingerprint = systemFingerprint,
            Usage = usagePayload,
        };
    }
}

public record TextChatSegment : ChatSegment
{
    public required string Text { get; init; }
}

public record ThinkChatSegment : ChatSegment
{
    public required string Think { get; init; }

    public string? Signature { get; init; }
}

public record UsageChatSegment : ChatSegment
{
    public required ChatTokenUsage Usage { get; init; }
}

public sealed record FinishReasonChatSegment : ChatSegment
{
    public required DBFinishReason? FinishReason { get; init; }
}

public static class ChatSegmentExtensions
{
    public static void AddMerged(this List<ChatSegment> items, ChatSegment item)
    {
        if (items.Count == 0)
        {
            items.Add(item);
            return;
        }

        ChatSegment last = items[^1];
        switch (last)
        {
            case TextChatSegment lastText when item is TextChatSegment curText:
                items[^1] = lastText with { Text = lastText.Text + curText.Text };
                break;

            case ThinkChatSegment lastThink when item is ThinkChatSegment curThink:
                string? signature = lastThink.Signature switch
                {
                    null => curThink.Signature,
                    _ => lastThink.Signature + curThink.Signature
                };
                items[^1] = lastThink with
                {
                    Think = lastThink.Think + curThink.Think,
                    Signature = signature
                };
                break;

            case ToolCallSegment lastTool when item is ToolCallSegment curTool && lastTool.Index == curTool.Index:
                items[^1] = lastTool with
                {
                    Arguments = (lastTool.Arguments ?? "") + (curTool.Arguments ?? ""),
                    Id = lastTool.Id ?? curTool.Id,
                    Name = lastTool.Name ?? curTool.Name
                };
                break;

            default:
                items.Add(item);
                break;
        }
    }

    public static OpenAIDelta ToOpenAIDelta(this ICollection<ChatSegment> items)
    {
        return new OpenAIDelta
        {
            Content = GetText(items),
            ReasoningContent = GetThink(items),
            Image = items.OfType<ImageChatSegment>().FirstOrDefault(),
            ToolCalls = items.OfType<ToolCallSegment>().Select(x => new OpenAIToolCallSegment
            {
                Id = x.Id,
                Function = new OpenAIToolCallSegmentFunction()
                {
                    Arguments = x.Arguments!,
                    Name = x.Name,
                },
                Index = x.Index,
            }).ToArray() switch { { Length: 0 } => null, var x => x }
        };
    }

        public static OpenAIFullResponse OpenAIFullResponse(this ICollection<ChatSegment> items, string role, object? refusal)
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

    public static string? GetText(this ICollection<ChatSegment> items)
    {
        return string.Concat(items.OfType<TextChatSegment>().Select(x => x.Text)) switch { "" => null, var x => x };
    }

    public static string? GetThink(this ICollection<ChatSegment> items)
    {
        return string.Concat(items.OfType<ThinkChatSegment>().Select(x => x.Think)) switch { "" => null, var x => x };
    }

    public static FullToolCall[]? GetToolCalls(this ICollection<ChatSegment> items)
    {
        return ToolCall.From(items.OfType<ToolCallSegment>()).Select(x => x.ToOpenAI()).ToArray() switch { { Length: 0 } => null, var x => x };
    }

    public static ImageChatSegment[] GetImages(this ICollection<ChatSegment> items)
    {
        return [.. items.OfType<ImageChatSegment>()];
    }
}
