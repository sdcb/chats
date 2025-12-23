using DBFile = Chats.DB.File;

namespace Chats.BE.Services.Models.Neutral;

/// <summary>
/// Base class for neutral content that is independent of any third-party SDK or database model.
/// Use pattern matching (is/switch) to check the concrete type.
/// </summary>
public abstract record NeutralContent
{
    /// <summary>
    /// Optional cache control for this content block.
    /// </summary>
    public NeutralCacheControl? CacheControl { get; init; }

    /// <summary>
    /// Returns true if this content represents a file (FileUrl, FileBlob, or FileId).
    /// </summary>
    public bool IsFile => this is NeutralFileUrlContent or NeutralFileBlobContent or NeutralFileContent;
}

/// <summary>
/// Text content.
/// </summary>
public record NeutralTextContent : NeutralContent
{
    public required string Content { get; init; }

    public static NeutralTextContent Create(string content, NeutralCacheControl? cacheControl = null)
        => new() { Content = content, CacheControl = cacheControl };
}

/// <summary>
/// File URL content.
/// </summary>
public record NeutralFileUrlContent : NeutralContent
{
    public required string Url { get; init; }

    public static NeutralFileUrlContent Create(string url, NeutralCacheControl? cacheControl = null)
        => new() { Url = url, CacheControl = cacheControl };
}

/// <summary>
/// File blob (binary data) content.
/// </summary>
public record NeutralFileBlobContent : NeutralContent
{
    public required byte[] Data { get; init; }
    public required string MediaType { get; init; }

    public static NeutralFileBlobContent Create(byte[] data, string mediaType, NeutralCacheControl? cacheControl = null)
        => new() { Data = data, MediaType = mediaType, CacheControl = cacheControl };
}

/// <summary>
/// File reference by database object (only for App chat).
/// Will be converted to FileUrl/FileBlob before sending to API.
/// Allows passing file objects directly to avoid repeated database queries.
/// </summary>
public record NeutralFileContent : NeutralContent
{
    public required DBFile File { get; init; }

    public static NeutralFileContent Create(DBFile file)
        => new() { File = file };
}

/// <summary>
/// Think/reasoning content (for models with extended thinking).
/// </summary>
public record NeutralThinkContent : NeutralContent
{
    public required string Content { get; init; }
    /// <summary>
    /// Signature for Anthropic/Gemini/Response api thinking blocks (required for multi-step tool call).
    /// </summary>
    public string? Signature { get; init; }

    public static NeutralThinkContent Create(string content, string? signature = null, NeutralCacheControl? cacheControl = null)
        => new() { Content = content, Signature = signature, CacheControl = cacheControl };
}

/// <summary>
/// Tool call content (assistant requesting to use a tool).
/// </summary>
public record NeutralToolCallContent : NeutralContent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Parameters { get; init; }

    public static NeutralToolCallContent Create(string id, string name, string parameters, NeutralCacheControl? cacheControl = null)
        => new() { Id = id, Name = name, Parameters = parameters, CacheControl = cacheControl };
}

/// <summary>
/// Tool call response content (response from tool execution).
/// </summary>
public record NeutralToolCallResponseContent : NeutralContent
{
    public required string ToolCallId { get; init; }
    public required string Response { get; init; }
    public bool IsSuccess { get; init; } = true;
    public int DurationMs { get; init; } = 0;

    public static NeutralToolCallResponseContent Create(string toolCallId, string response, bool isSuccess = true, int durationMs = 0, NeutralCacheControl? cacheControl = null)
        => new() { ToolCallId = toolCallId, Response = response, IsSuccess = isSuccess, DurationMs = durationMs, CacheControl = cacheControl };
}

/// <summary>
/// Error content.
/// </summary>
public record NeutralErrorContent : NeutralContent
{
    public required string Content { get; init; }

    public static NeutralErrorContent Create(string content, NeutralCacheControl? cacheControl = null)
        => new() { Content = content, CacheControl = cacheControl };
}
