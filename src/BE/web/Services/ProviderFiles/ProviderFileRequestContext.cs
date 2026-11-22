using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using DBFile = Chats.DB.File;

namespace Chats.BE.Services.ProviderFiles;

public enum ProviderFileRetryAction
{
    None,
    /// <summary>Mark the used mappings invalid, upload again and retry the request once.</summary>
    ReuploadAndRetry,
    /// <summary>Mark the used mappings invalid and retry with inline (base64/URL) content.</summary>
    FallbackInline,
}

/// <summary>
/// Per chat-request state for provider file references: resolves file ids during PreProcess and decides how to
/// recover when the upstream rejects them.
/// </summary>
public sealed class ProviderFileRequestContext
{
    private readonly ProviderFileService _service;
    private readonly ILogger _logger;
    private readonly List<ProviderFileReference> _used = [];

    internal ProviderFileRequestContext(ProviderFileService service, ModelKeySnapshot modelKey, ProviderFileTarget target, ILogger logger)
    {
        _service = service;
        _logger = logger;
        ModelKey = modelKey;
        Target = target;
    }

    public ModelKeySnapshot ModelKey { get; }

    public ProviderFileTarget Target { get; }

    /// <summary>False after falling back to inline content; PreProcess then keeps the legacy behaviour.</summary>
    public bool RemoteEnabled { get; private set; } = true;

    /// <summary>Provider file references used by the current attempt.</summary>
    public IReadOnlyList<ProviderFileReference> UsedReferences => _used;

    public void BeginAttempt() => _used.Clear();

    /// <summary>
    /// Returns a provider file reference for the file, or null when the caller should use inline content.
    /// </summary>
    public async Task<NeutralContent?> TryCreateContentAsync(DBFile file, NeutralCacheControl? cacheControl, CancellationToken cancellationToken)
    {
        if (!RemoteEnabled || !_service.IsCandidate(file))
        {
            return null;
        }

        ProviderFileReference? reference = await _service.TryGetOrUploadAsync(file, ModelKey, Target, cancellationToken);
        if (reference == null)
        {
            return null;
        }

        _used.Add(reference);
        return NeutralProviderFileContent.Create(reference.RemoteFileId, file.MediaType, cacheControl);
    }

    public ProviderFileRetryAction Decide(Exception exception, bool yieldedAny, int attempt, bool cancellationRequested)
        => DecideRetry(exception, yieldedAny, _used.Count, attempt, cancellationRequested);

    /// <summary>
    /// Retry only when nothing has been streamed yet, the attempt actually referenced provider files and the upstream
    /// error looks like an unknown/expired/invalid file id. First failure re-uploads, second falls back to inline.
    /// </summary>
    public static ProviderFileRetryAction DecideRetry(Exception exception, bool yieldedAny, int usedReferenceCount, int attempt, bool cancellationRequested)
    {
        if (yieldedAny || usedReferenceCount == 0 || cancellationRequested || !IsInvalidFileError(exception))
        {
            return ProviderFileRetryAction.None;
        }

        return attempt switch
        {
            0 => ProviderFileRetryAction.ReuploadAndRetry,
            1 => ProviderFileRetryAction.FallbackInline,
            _ => ProviderFileRetryAction.None,
        };
    }

    public static bool IsInvalidFileError(Exception exception)
    {
        if (exception is not RawChatServiceException raw || raw.StatusCode is not (400 or 403 or 404 or 410 or 422 or 200))
        {
            return false;
        }

        string body = raw.Body ?? string.Empty;
        if (!body.Contains("file", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] markers =
        [
            "not found", "not_found", "does not exist", "doesn't exist", "not exist", "no such file",
            "expired", "invalid file", "invalid_file", "file_id", "file id", "deleted", "unknown file",
        ];
        return markers.Any(m => body.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ApplyRetryAsync(ProviderFileRetryAction action, Exception exception, CancellationToken cancellationToken)
    {
        if (action == ProviderFileRetryAction.None)
        {
            return;
        }

        string body = (exception as RawChatServiceException)?.Body ?? string.Empty;
        List<ProviderFileReference> mentioned = [.. _used.Where(x => body.Contains(x.RemoteFileId, StringComparison.Ordinal))];
        List<long> toInvalidate = [.. (mentioned.Count > 0 ? mentioned : _used).Select(x => x.MappingId).Distinct()];

        _logger.LogWarning(exception,
            "Upstream rejected provider file reference(s); invalidating {Count} mapping(s) and {Action}. ModelKeyId={ModelKeyId}, Protocol={Protocol}",
            toInvalidate.Count, action, ModelKey.ModelKeyId, Target.Protocol);

        try
        {
            await _service.InvalidateAsync(toInvalidate, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to invalidate provider file mappings");
        }

        if (action == ProviderFileRetryAction.FallbackInline)
        {
            RemoteEnabled = false;
        }
    }
}
