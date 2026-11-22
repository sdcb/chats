namespace Chats.BE.Services.ProviderFiles;

/// <summary>
/// Configuration section "ProviderFiles". Controls uploading user files to the model provider's Files API
/// and referencing them by file id instead of sending base64/URL content on every request.
/// </summary>
public sealed class ProviderFileOptions
{
    public const string SectionName = "ProviderFiles";

    /// <summary>Global switch. When false all requests keep the inline (base64/URL) behaviour.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Model key ids for which provider file ids must never be used.</summary>
    public short[] DisabledModelKeyIds { get; set; } = [];

    /// <summary>Model provider ids (DBModelProvider) for which provider file ids must never be used.</summary>
    public short[] DisabledProviderIds { get; set; } = [];

    /// <summary>
    /// Lifetime requested from OpenAI-compatible Files APIs (expires_after). 0 or negative = keep permanently.
    /// Providers accept 3600..2592000 seconds.
    /// </summary>
    public int OpenAIExpiresAfterSeconds { get; set; } = 7 * 24 * 3600;

    /// <summary>A mapping is treated as expired this many seconds before its ExpiresAt.</summary>
    public int ExpirySafetyMarginSeconds { get; set; } = 600;

    /// <summary>After an upload fails because the endpoint looks unsupported, skip uploads for this key for this long.</summary>
    public int UnsupportedBackoffSeconds { get; set; } = 3600;

    /// <summary>Files larger than this are always sent inline.</summary>
    public long MaxFileBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Interval of the background job that marks expired mappings. 0 disables it.</summary>
    public int CleanupIntervalSeconds { get; set; } = 3600;
}
