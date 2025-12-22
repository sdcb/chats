namespace Chats.Web.Services.Models.Neutral;

/// <summary>
/// Neutral message that is independent of any third-party SDK or database model.
/// </summary>
public record NeutralMessage
{
    public required NeutralChatRole Role { get; init; }
    public required IList<NeutralContent> Contents { get; init; }

    public static NeutralMessage FromUser(params NeutralContent[] contents)
    {
        return new NeutralMessage
        {
            Role = NeutralChatRole.User,
            Contents = contents
        };
    }

    public static NeutralMessage FromAssistant(params NeutralContent[] contents)
    {
        return new NeutralMessage
        {
            Role = NeutralChatRole.Assistant,
            Contents = contents
        };
    }

    public static NeutralMessage FromTool(params NeutralContent[] contents)
    {
        return new NeutralMessage
        {
            Role = NeutralChatRole.Tool,
            Contents = contents
        };
    }

    public static NeutralMessage FromUserText(string text, NeutralCacheControl? cacheControl = null)
    {
        return FromUser(NeutralTextContent.Create(text, cacheControl));
    }

    public static NeutralMessage FromAssistantText(string text, NeutralCacheControl? cacheControl = null)
    {
        return FromAssistant(NeutralTextContent.Create(text, cacheControl));
    }
}
