namespace Chats.BE.Services.Models.Neutral;

/// <summary>
/// Neutral system message that is independent of any third-party SDK or database model.
/// Supports both simple string and structured content blocks with cache control.
/// </summary>
public record NeutralSystemMessage
{
    /// <summary>
    /// System message contents. Can contain multiple content blocks with cache control.
    /// </summary>
    public required IList<NeutralSystemContent> Contents { get; init; }

    /// <summary>
    /// Creates a simple system message from a single string.
    /// </summary>
    public static NeutralSystemMessage FromText(string text, NeutralCacheControl? cacheControl = null)
    {
        return new NeutralSystemMessage
        {
            Contents = [new NeutralSystemContent { Text = text, CacheControl = cacheControl }]
        };
    }

    /// <summary>
    /// Creates a system message from multiple content blocks.
    /// </summary>
    public static NeutralSystemMessage FromContents(params NeutralSystemContent[] contents)
    {
        return new NeutralSystemMessage { Contents = contents };
    }

    /// <summary>
    /// Gets the combined text content (for providers that don't support structured system messages).
    /// </summary>
    public string? GetCombinedText()
    {
        if (Contents.Count == 0) return null;

        string combined = string.Join("\n\n", Contents.Select(c => c.Text));
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }
}

/// <summary>
/// Individual content block within a system message.
/// </summary>
public record NeutralSystemContent
{
    /// <summary>
    /// Text content.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Optional cache control for this content block.
    /// </summary>
    public NeutralCacheControl? CacheControl { get; init; }
}
