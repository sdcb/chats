using Chats.BE.Services.FileServices;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using DBFile = Chats.DB.File;

namespace Chats.BE.Services.ProviderFiles;

/// <summary>
/// Maintains the mapping between local files and the remote file ids returned by model providers' Files APIs
/// (table ProviderFileMapping), uploading on demand and reusing active mappings across requests.
/// Singleton: every DB operation uses its own scope so it is safe to call from parallel chat runs.
/// </summary>
public class ProviderFileService(
    IServiceScopeFactory scopeFactory,
    IFileServiceFactory fileServiceFactory,
    IProviderFileClientFactory clientFactory,
    IOptionsMonitor<ProviderFileOptions> options,
    ILogger<ProviderFileService> logger,
    TimeProvider? timeProvider = null)
{
    private const int LockStripes = 64;
    private static readonly TimeSpan RemoteDeleteTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim[] _locks = [.. Enumerable.Range(0, LockStripes).Select(_ => new SemaphoreSlim(1, 1))];
    private readonly ConcurrentDictionary<(short ModelKeyId, string Protocol), DateTime> _unsupportedUntil = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public ProviderFileOptions Options => options.CurrentValue;

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Returns the Files API target for this model if provider file ids should be used, otherwise null.
    /// </summary>
    public ProviderFileTarget? ResolveTarget(ModelKeySnapshot modelKey, DBApiType apiType)
    {
        ProviderFileOptions opt = Options;
        if (!opt.Enabled
            || opt.DisabledModelKeyIds.Contains(modelKey.ModelKeyId)
            || opt.DisabledProviderIds.Contains(modelKey.ModelProviderId)
            || string.IsNullOrEmpty(modelKey.Secret))
        {
            return null;
        }

        ProviderFileTarget? target = ProviderFileCapabilities.Resolve((DBModelProvider)modelKey.ModelProviderId, apiType);
        if (target == null || clientFactory.GetClient(target.Protocol) == null)
        {
            return null;
        }

        if (_unsupportedUntil.TryGetValue((modelKey.ModelKeyId, target.Protocol), out DateTime until) && until > UtcNow)
        {
            return null;
        }

        return target;
    }

    public ProviderFileRequestContext? CreateRequestContext(ModelSnapshot snapshot, bool serviceSupportsReferences)
    {
        if (!serviceSupportsReferences || snapshot.ModelKeySnapshot == null)
        {
            return null;
        }

        ProviderFileTarget? target = ResolveTarget(snapshot.ModelKeySnapshot, (DBApiType)snapshot.ApiTypeId);
        return target == null ? null : new ProviderFileRequestContext(this, snapshot.ModelKeySnapshot, target, logger);
    }

    public bool IsCandidate(DBFile file)
    {
        return ProviderFileCapabilities.IsSupportedMediaType(file.MediaType)
            && (file.Size <= 0 || file.Size <= Options.MaxFileBytes);
    }

    public static bool IsUsable(ProviderFileMapping mapping, DateTime utcNow, TimeSpan safetyMargin)
    {
        if (mapping.Status != (byte)ProviderFileStatus.Active)
        {
            return false;
        }

        return mapping.ExpiresAt == null || mapping.ExpiresAt.Value - safetyMargin > utcNow;
    }

    /// <summary>
    /// Like <see cref="GetOrUploadAsync"/> but never throws (except for cancellation): returns null so that the caller
    /// falls back to inline content.
    /// </summary>
    public async Task<ProviderFileReference?> TryGetOrUploadAsync(DBFile file, ModelKeySnapshot modelKey, ProviderFileTarget target, CancellationToken cancellationToken)
    {
        try
        {
            return await GetOrUploadAsync(file, modelKey, target, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex is ProviderFileApiException { LooksUnsupported: true } or ProviderFileApiException { StatusCode: 401 or 403 })
            {
                _unsupportedUntil[(modelKey.ModelKeyId, target.Protocol)] = UtcNow.AddSeconds(Math.Max(0, Options.UnsupportedBackoffSeconds));
            }

            logger.LogWarning(ex,
                "Provider file upload failed, falling back to inline content. FileId={FileId}, ModelKeyId={ModelKeyId}, Protocol={Protocol}",
                file.Id, modelKey.ModelKeyId, target.Protocol);
            return null;
        }
    }

    public async Task<ProviderFileReference> GetOrUploadAsync(DBFile file, ModelKeySnapshot modelKey, ProviderFileTarget target, CancellationToken cancellationToken)
    {
        IProviderFileClient client = clientFactory.GetClient(target.Protocol)
            ?? throw new InvalidOperationException($"No provider file client for protocol {target.Protocol}.");
        TimeSpan margin = TimeSpan.FromSeconds(Math.Max(0, Options.ExpirySafetyMarginSeconds));

        ProviderFileReference? existing = await TryReuseAsync(file.Id, modelKey.ModelKeyId, target, margin, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        SemaphoreSlim gate = _locks[(int)((uint)HashCode.Combine(file.Id, modelKey.ModelKeyId, target.Protocol, target.Purpose) % LockStripes)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = await TryReuseAsync(file.Id, modelKey.ModelKeyId, target, margin, cancellationToken);
            if (existing != null)
            {
                return existing;
            }

            byte[] content = await ReadFileAsync(file, cancellationToken);
            TimeSpan? expiresAfter = target.Protocol == ProviderFileProtocols.OpenAI
                && Options.OpenAIExpiresAfterSeconds > 0
                && ProviderFileCapabilities.SupportsExpiresAfter((DBModelProvider)modelKey.ModelProviderId)
                ? TimeSpan.FromSeconds(Options.OpenAIExpiresAfterSeconds)
                : null;
            ProviderUploadedFile uploaded = await client.UploadAsync(
                modelKey,
                new ProviderFileUploadRequest(content, file.FileName, file.MediaType, target.Purpose, expiresAfter),
                cancellationToken);

            DateTime now = UtcNow;
            DateTime? expiresAt = uploaded.ExpiresAt ?? (expiresAfter.HasValue ? now + expiresAfter.Value : null);
            return await RecordAsync(file.Id, modelKey, target, client, uploaded.Id, now, expiresAt, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ProviderFileReference?> TryReuseAsync(int fileId, short modelKeyId, ProviderFileTarget target, TimeSpan margin, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        ProviderFileMapping? mapping = await db.ProviderFileMappings
            .Where(x => x.FileId == fileId
                && x.ModelKeyId == modelKeyId
                && x.Protocol == target.Protocol
                && x.Purpose == target.Purpose
                && x.Status == (byte)ProviderFileStatus.Active)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (mapping == null)
        {
            return null;
        }

        DateTime now = UtcNow;
        if (!IsUsable(mapping, now, margin))
        {
            mapping.Status = (byte)ProviderFileStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        mapping.LastUsedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return new ProviderFileReference(mapping.Id, mapping.RemoteFileId);
    }

    private async Task<ProviderFileReference> RecordAsync(
        int fileId, ModelKeySnapshot modelKey, ProviderFileTarget target, IProviderFileClient client,
        string remoteFileId, DateTime now, DateTime? expiresAt, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        ProviderFileMapping mapping = new()
        {
            FileId = fileId,
            ModelKeyId = modelKey.ModelKeyId,
            Protocol = target.Protocol,
            Purpose = target.Purpose,
            RemoteFileId = remoteFileId,
            Status = (byte)ProviderFileStatus.Active,
            UploadedAt = now,
            ExpiresAt = expiresAt,
            LastUsedAt = now,
        };
        db.ProviderFileMappings.Add(mapping);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ProviderFileReference(mapping.Id, remoteFileId);
        }
        catch (DbUpdateException ex)
        {
            // Another instance won the race on the filtered unique index: use its mapping and drop our upload.
            ProviderFileReference? winner = await TryReuseAsync(fileId, modelKey.ModelKeyId, target, TimeSpan.Zero, cancellationToken);
            if (winner == null)
            {
                throw;
            }

            logger.LogInformation(ex, "Concurrent provider file upload detected, reusing mapping {MappingId}", winner.MappingId);
            await TryDeleteRemoteAsync(client, modelKey, remoteFileId);
            return winner;
        }
    }

    private async Task<byte[]> ReadFileAsync(DBFile file, CancellationToken cancellationToken)
    {
        FileService? fileService = file.FileService;
        if (fileService == null)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            fileService = await db.FileServices.FirstAsync(x => x.Id == file.FileServiceId, cancellationToken);
        }

        IFileService fs = fileServiceFactory.Create(fileService);
        await using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    public async Task InvalidateAsync(IReadOnlyCollection<long> mappingIds, CancellationToken cancellationToken)
    {
        if (mappingIds.Count == 0)
        {
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        List<ProviderFileMapping> mappings = await db.ProviderFileMappings
            .Where(x => mappingIds.Contains(x.Id) && x.Status == (byte)ProviderFileStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (ProviderFileMapping mapping in mappings)
        {
            mapping.Status = (byte)ProviderFileStatus.Invalid;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Marks all active mappings of a local file as deleted and best-effort deletes the remote copies.
    /// Call before deleting the local file (the FK cascade removes the rows afterwards). Never throws.
    /// </summary>
    public Task ReleaseForFileAsync(int fileId, CancellationToken cancellationToken)
        => ReleaseAsync(x => x.FileId == fileId, cancellationToken);

    /// <summary>
    /// Marks all active mappings of a model key as deleted and best-effort deletes the remote copies.
    /// Call before deleting the model key (credentials are needed for remote deletion). Never throws.
    /// </summary>
    public Task ReleaseForModelKeyAsync(short modelKeyId, CancellationToken cancellationToken)
        => ReleaseAsync(x => x.ModelKeyId == modelKeyId, cancellationToken);

    private async Task ReleaseAsync(System.Linq.Expressions.Expression<Func<ProviderFileMapping, bool>> filter, CancellationToken cancellationToken)
    {
        List<(ModelKeySnapshot Key, string Protocol, string RemoteFileId)> toDelete = [];
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            List<ProviderFileMapping> mappings = await db.ProviderFileMappings
                .Include(x => x.ModelKey).ThenInclude(x => x.CurrentSnapshot)
                .Where(filter)
                .Where(x => x.Status != (byte)ProviderFileStatus.Deleted)
                .ToListAsync(cancellationToken);
            foreach (ProviderFileMapping mapping in mappings)
            {
                if (mapping.Status == (byte)ProviderFileStatus.Active || mapping.Status == (byte)ProviderFileStatus.Expired)
                {
                    toDelete.Add((mapping.ModelKey.CurrentSnapshot, mapping.Protocol, mapping.RemoteFileId));
                }
                mapping.Status = (byte)ProviderFileStatus.Deleted;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to mark provider file mappings as deleted");
            return;
        }

        await Parallel.ForEachAsync(toDelete, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (item, _) =>
        {
            IProviderFileClient? client = clientFactory.GetClient(item.Protocol);
            if (client != null)
            {
                await TryDeleteRemoteAsync(client, item.Key, item.RemoteFileId);
            }
        });
    }

    private async Task TryDeleteRemoteAsync(IProviderFileClient client, ModelKeySnapshot modelKey, string remoteFileId)
    {
        try
        {
            using CancellationTokenSource cts = new(RemoteDeleteTimeout);
            await client.DeleteAsync(modelKey, remoteFileId, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Best-effort remote file deletion failed. ModelKeyId={ModelKeyId}, RemoteFileId={RemoteFileId}", modelKey.ModelKeyId, remoteFileId);
        }
    }

    /// <summary>Marks active mappings whose ExpiresAt has passed as expired. Returns the number of rows updated.</summary>
    public async Task<int> MarkExpiredAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        DateTime now = UtcNow;
        List<ProviderFileMapping> expired = await db.ProviderFileMappings
            .Where(x => x.Status == (byte)ProviderFileStatus.Active && x.ExpiresAt != null && x.ExpiresAt <= now)
            .Take(1000)
            .ToListAsync(cancellationToken);
        foreach (ProviderFileMapping mapping in expired)
        {
            mapping.Status = (byte)ProviderFileStatus.Expired;
        }
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
