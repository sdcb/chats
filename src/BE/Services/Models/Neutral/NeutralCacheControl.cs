namespace Chats.BE.Services.Models.Neutral;

/// <summary>
/// Neutral cache control that can be applied to content blocks.
/// Currently supports Anthropic-style cache control (ephemeral cache breakpoints).
/// </summary>
public record NeutralCacheControl
{
    /// <summary>
    /// Cache type. For Anthropic, this is "ephemeral".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Creates an ephemeral cache control (Anthropic-style).
    /// </summary>
    public static NeutralCacheControl Ephemeral => new() { Type = "ephemeral" };
}
