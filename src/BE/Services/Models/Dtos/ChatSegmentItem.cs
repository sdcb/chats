using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;

namespace Chats.BE.Services.Models.Dtos;

public abstract record ChatSegmentItem
{
    public abstract Task<MessageContent> ToDB(DBFileService dbFileService, CancellationToken cancellationToken = default);

    public static ChatSegmentItem FromText(string text)
    {
        return new TextChatSegment { Text = text };
    }

    public static ChatSegmentItem FromThink(string think)
    {
        return new ThinkChatSegment { Think = think };
    }

    public static ChatSegmentItem FromImage(ImageChatSegment image)
    {
        return image;
    }

    public static List<ChatSegmentItem> FromTextAndThink(string? text, string? think)
    {
        List<ChatSegmentItem> segments = [];
        if (text != null)
        {
            segments.Add(FromText(text));
        }
        if (think != null)
        {
            segments.Add(FromThink(think));
        }
        return segments;
    }
}

public record TextChatSegment : ChatSegmentItem
{
    public required string Text { get; init; }

    public override Task<MessageContent> ToDB(DBFileService dbFileService, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MessageContent.FromText(Text));
    }
}

public record ThinkChatSegment : ChatSegmentItem
{
    public required string Think { get; init; }

    public override Task<MessageContent> ToDB(DBFileService dbFileService, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MessageContent.FromThink(Think));
    }
}

public static class ChatSegmentItemExtensions
{
    public static async Task<MessageContent[]> ToDB(this ChatSegmentItem[] items, DBFileService dbFileService, CancellationToken cancellationToken = default)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToDB(dbFileService, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static OpenAIDelta ToOpenAIDelta(this ICollection<ChatSegmentItem> items)
    {
        return new OpenAIDelta
        {
            Content = GetText(items),
            ReasoningContent = GetThink(items),
            Image = items.OfType<ImageChatSegment>().FirstOrDefault(),
        };
    }

    public static List<ChatSegmentItem> Combine(this ICollection<ChatSegmentItem> items)
    {
        List<ChatSegmentItem> segments = new(items.Count);

        foreach (ChatSegmentItem item in items)
        {
            if (segments.Count == 0)
            {
                segments.Add(item);
                continue;
            }

            // 否则尝试合并
            var last = segments[^1];
            if (last is TextChatSegment lastText && item is TextChatSegment currentText)
            {
                // 两个连续 Text，合并文本
                segments[^1] = lastText with { Text = lastText.Text + currentText.Text };
            }
            else if (last is ThinkChatSegment lastThink && item is ThinkChatSegment currentThink)
            {
                // 两个连续 Think，合并文本
                segments[^1] = lastThink with { Think = lastThink.Think + currentThink.Think };
            }
            else
            {
                // 其他情况都不合并，直接添加
                segments.Add(item);
            }
        }

        return segments;
    }

    public static OpenAIFullResponse OpenAIFullResponse(this ICollection<ChatSegmentItem> items, string role, object? refusal)
    {
        List<ChatSegmentItem> full = items.Combine();
        return new OpenAIFullResponse
        {
            Role = role,
            Content = GetText(full),
            ReasoningContent = GetThink(full),
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