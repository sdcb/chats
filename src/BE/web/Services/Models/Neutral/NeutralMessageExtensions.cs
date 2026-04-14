namespace Chats.BE.Services.Models.Neutral;

public sealed record NeutralToolResponseGroup
{
    public required NeutralToolCallResponseContent ToolResponse { get; init; }
    public required IList<NeutralContent> AttachedContents { get; init; }
}

/// <summary>
/// Extension methods for NeutralMessage.
/// </summary>
public static class NeutralMessageExtensions
{
    /// <summary>
    /// Gets the last user message from a list of messages.
    /// </summary>
    public static NeutralMessage? LastUserMessage(this IList<NeutralMessage> messages)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == NeutralChatRole.User)
            {
                return messages[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all text content from a message.
    /// </summary>
    public static string GetTextContent(this NeutralMessage message)
    {
        return string.Join("", message.Contents
            .OfType<NeutralTextContent>()
            .Select(t => t.Content));
    }

    /// <summary>
    /// Gets the first text content from a message, or null if none exists.
    /// </summary>
    public static string? GetFirstTextContent(this NeutralMessage message)
    {
        return message.Contents
            .OfType<NeutralTextContent>()
            .Select(t => t.Content)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets all file blob contents from a message.
    /// </summary>
    public static IEnumerable<NeutralFileBlobContent> GetFileBlobs(this NeutralMessage message)
    {
        return message.Contents.OfType<NeutralFileBlobContent>();
    }

    /// <summary>
    /// Gets all file URL contents from a message.
    /// </summary>
    public static IEnumerable<NeutralFileUrlContent> GetFileUrls(this NeutralMessage message)
    {
        return message.Contents.OfType<NeutralFileUrlContent>();
    }

    /// <summary>
    /// Gets all tool call contents from a message.
    /// </summary>
    public static IEnumerable<NeutralToolCallContent> GetToolCalls(this NeutralMessage message)
    {
        return message.Contents.OfType<NeutralToolCallContent>();
    }

    /// <summary>
    /// Gets all tool call response contents from a message.
    /// </summary>
    public static IEnumerable<NeutralToolCallResponseContent> GetToolCallResponses(this NeutralMessage message)
    {
        return message.Contents.OfType<NeutralToolCallResponseContent>();
    }

    /// <summary>
    /// Gets the first tool call response content, or null if none exists.
    /// </summary>
    public static NeutralToolCallResponseContent? GetFirstToolCallResponse(this NeutralMessage message)
    {
        return message.Contents.OfType<NeutralToolCallResponseContent>().FirstOrDefault();
    }

    /// <summary>
    /// Groups tool response contents with the content blocks that immediately follow them.
    /// This preserves associations such as tool result text plus generated artifact images.
    /// </summary>
    public static IReadOnlyList<NeutralToolResponseGroup> GetToolResponseGroups(this NeutralMessage message)
    {
        List<NeutralToolResponseGroup> groups = [];
        List<NeutralContent> leadingContents = [];
        NeutralToolResponseGroup? currentGroup = null;

        foreach (NeutralContent content in message.Contents)
        {
            if (content is NeutralToolCallResponseContent toolResponse)
            {
                currentGroup = new NeutralToolResponseGroup
                {
                    ToolResponse = toolResponse,
                    AttachedContents = []
                };
                groups.Add(currentGroup);
                continue;
            }

            if (currentGroup == null)
            {
                leadingContents.Add(content);
            }
            else
            {
                currentGroup.AttachedContents.Add(content);
            }
        }

        if (groups.Count > 0 && leadingContents.Count > 0)
        {
            for (int i = leadingContents.Count - 1; i >= 0; i--)
            {
                groups[0].AttachedContents.Insert(0, leadingContents[i]);
            }
        }

        return groups;
    }
}
