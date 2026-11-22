using System.Collections.Concurrent;
using Chats.DB;
using Chats.DB.Enums;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Containers;

/// <summary>
/// Backend-side lifecycle helpers shared by user APIs, the port-mapping service,
/// background cleanup and admin actions: docker volumes, (re)creating permanent
/// containers with their mounts/port bindings, and releasing backend resources.
/// Not bound to the current HTTP user.
/// </summary>
public sealed class ContainerRuntimeManager(
    ChatsDB db,
    ContainerBackendFactory backends,
    ILogger<ContainerRuntimeManager> logger)
{
    public const string LabelOwnerUserId = "chats.owner-user-id";
    public const string LabelVolumeId = "chats.volume-id";
    public const string LabelResourceId = "chats.resource-id";
    public const string LabelKind = "chats.kind";

    private static readonly ConcurrentDictionary<long, SemaphoreSlim> ResourceLocks = new();

    private readonly ChatsDB _db = db;
    private readonly ContainerBackendFactory _backends = backends;
    private readonly ILogger<ContainerRuntimeManager> _logger = logger;

    /// <summary>
    /// Serializes backend changes (recreate/mount/port mapping) for one container resource within this process.
    /// </summary>
    public static async Task<IDisposable> LockResourceAsync(long resourceId, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = ResourceLocks.GetOrAdd(resourceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    public static string NewBackendVolumeName() => $"chats-vol-{Guid.NewGuid():N}";

    public static bool IsManagedVolumeName(string? name) => name is not null && name.StartsWith("chats-vol-", StringComparison.Ordinal);

    public static void EnsureDockerNode(ContainerRuntimeNode node)
    {
        if (!node.IsEnabled) throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeUnavailable, "Runtime node is disabled.");
        if (node.BackendType != (byte)DBContainerBackendType.Docker) throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeNotImplemented, "This runtime backend is not implemented yet.");
    }

    /// <summary>
    /// Creates the docker named volume for <paramref name="volume"/> (assigning a BackendVolumeId when missing).
    /// The caller is responsible for saving the entity.
    /// </summary>
    public async Task EnsureBackendVolumeAsync(ContainerVolume volume, ContainerRuntimeNode node, CancellationToken cancellationToken)
    {
        EnsureDockerNode(node);
        volume.BackendVolumeId ??= NewBackendVolumeName();
        Dictionary<string, string> labels = new()
        {
            [LabelOwnerUserId] = volume.OwnerUserId.ToString(),
            [LabelKind] = volume.IsStandalone ? "standalone-volume" : "internal-volume",
        };
        if (volume.Id != 0) labels[LabelVolumeId] = volume.Id.ToString();
        try
        {
            await _backends.Get(node).CreateVolumeAsync(volume.BackendVolumeId, labels, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create docker volume {Volume} on node {Node}", volume.BackendVolumeId, node.Name);
            throw new ContainerResourceException(ContainerResourceErrorCodes.BackendOperationFailed, "Backend volume creation failed.");
        }
    }

    /// <summary>
    /// Best-effort removal of the docker volume backing <paramref name="volume"/>. Only volumes created by
    /// Chats (named <c>chats-vol-*</c>) are removed; admin-bound external volumes are left untouched.
    /// </summary>
    public async Task DeleteBackendVolumeAsync(ContainerVolume volume, ContainerRuntimeNode node, CancellationToken cancellationToken)
    {
        if (!IsManagedVolumeName(volume.BackendVolumeId) || node.BackendType != (byte)DBContainerBackendType.Docker) return;
        try
        {
            await _backends.Get(node).DeleteVolumeAsync(volume.BackendVolumeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete docker volume {Volume} on node {Node}", volume.BackendVolumeId, node.Name);
        }
    }

    /// <summary>
    /// Pure function: computes the backend create options (mounts, port bindings, labels) for a container resource.
    /// </summary>
    public static ContainerCreateOptions BuildCreateOptions(
        ContainerResource resource,
        string workDir,
        ContainerVolume? internalVolume,
        IEnumerable<ContainerVolumeMount> mounts,
        IEnumerable<ContainerPortMapping> portMappings)
    {
        List<ContainerVolumeMountSpec> mountSpecs = [];
        HashSet<string> targets = new(StringComparer.Ordinal);
        foreach (ContainerVolumeMount mount in mounts
            .Where(x => x.IsActive && x.Volume is { IsActive: true } && !string.IsNullOrWhiteSpace(x.Volume.BackendVolumeId))
            .OrderBy(x => x.Id))
        {
            string target = NormalizeContainerPath(mount.ContainerPath);
            if (targets.Add(target)) mountSpecs.Add(new ContainerVolumeMountSpec(mount.Volume.BackendVolumeId!, target, mount.IsReadOnly));
        }
        if (internalVolume is { IsActive: true } && !string.IsNullOrWhiteSpace(internalVolume.BackendVolumeId))
        {
            string target = NormalizeContainerPath(workDir);
            if (targets.Add(target)) mountSpecs.Insert(0, new ContainerVolumeMountSpec(internalVolume.BackendVolumeId!, target, false));
        }

        List<ContainerPortBindingSpec> ports = [.. portMappings
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new ContainerPortBindingSpec(x.ContainerPort, x.HostPort, x.Protocol))];

        Dictionary<string, string> labels = new()
        {
            [LabelOwnerUserId] = resource.OwnerUserId.ToString(),
            [LabelKind] = resource.IsPermanent ? "permanent-container" : "temporary-container",
        };
        if (resource.Id != 0) labels[LabelResourceId] = resource.Id.ToString();
        return new ContainerCreateOptions
        {
            Mounts = mountSpecs,
            PortBindings = ports,
            Labels = labels,
        };
    }

    public static string NormalizeContainerPath(string path)
    {
        string trimmed = path.Trim().Replace('\\', '/');
        while (trimmed.Length > 1 && trimmed.EndsWith('/')) trimmed = trimmed[..^1];
        return trimmed;
    }

    public static bool IsValidContainerPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string normalized = NormalizeContainerPath(path);
        if (!normalized.StartsWith('/') || normalized == "/") return false;
        if (normalized.Length > 512) return false;
        if (normalized.Split('/').Any(x => x == ".." || x == ".")) return false;
        return !normalized.Any(c => char.IsControl(c) || c == ':' || c == ',');
    }

    public async Task<ContainerCreateOptions> LoadCreateOptionsAsync(ContainerResource resource, string workDir, CancellationToken cancellationToken)
    {
        ContainerVolume? internalVolume = await _db.ContainerVolumes
            .FirstOrDefaultAsync(x => x.ContainerResourceId == resource.Id && !x.IsStandalone && x.IsActive, cancellationToken);
        List<ContainerVolumeMount> mounts = await _db.ContainerVolumeMounts
            .Include(x => x.Volume)
            .Where(x => x.ContainerResourceId == resource.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        List<ContainerPortMapping> mappings = await _db.ContainerPortMappings
            .Where(x => x.ContainerResourceId == resource.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        return BuildCreateOptions(resource, workDir, internalVolume, mounts, mappings);
    }

    /// <summary>
    /// Re-creates the backend container of a permanent resource so that its current active
    /// mounts and port mappings take effect. Volumes and the ContainerResource row are preserved
    /// (BackendResourceId/Ip/ShellPrefix are updated). Data outside mounted volumes is lost.
    /// The previous running/stopped state is kept. On failure the old container is restored.
    /// Changes are saved.
    /// </summary>
    public async Task RecreateAsync(ContainerResource resource, CancellationToken cancellationToken)
    {
        if (resource.DeletedAt is not null) throw new ContainerResourceException(ContainerResourceErrorCodes.ContainerDeleted, "Container has been deleted.");
        if (!resource.IsPermanent) throw new ContainerResourceException(ContainerResourceErrorCodes.PermanentContainerRequired, "Only permanent containers can be reconfigured.");
        ContainerRuntimeNode node = resource.RuntimeNode ?? await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
        EnsureDockerNode(node);
        IDockerService docker = _backends.Get(node);
        ContainerCreateOptions options = await LoadCreateOptionsAsync(resource, docker.Config.WorkDir, cancellationToken);
        bool wasStopped = resource.StoppedAt is not null;
        string oldId = resource.BackendResourceId;

        if (!wasStopped)
        {
            try { await docker.StopContainerAsync(oldId, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to stop container {ContainerId} before recreation", oldId); }
        }

        ContainerInfo created;
        try
        {
            created = await docker.CreateContainerCoreAsync(resource.Image, ToLimits(resource), resource.BackendNetworkName, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recreate container resource {ResourceId} on node {Node}", resource.Id, node.Name);
            if (!wasStopped)
            {
                try { await docker.StartContainerAsync(oldId, CancellationToken.None); }
                catch (Exception restoreEx) { _logger.LogWarning(restoreEx, "Failed to restart previous container {ContainerId}", oldId); }
            }
            throw new ContainerResourceException(ContainerResourceErrorCodes.BackendOperationFailed, "Backend container recreation failed: " + ex.Message);
        }

        try { await docker.DeleteContainerAsync(oldId, CancellationToken.None); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete previous container {ContainerId} after recreation", oldId); }

        if (wasStopped)
        {
            try { await docker.StopContainerAsync(created.ContainerId, CancellationToken.None); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to stop recreated container {ContainerId}", created.ContainerId); }
        }

        resource.BackendResourceId = created.ContainerId;
        resource.Ip = created.Ip;
        resource.ShellPrefix = created.ShellPrefix == null ? resource.ShellPrefix : string.Join(',', created.ShellPrefix);
        resource.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Deletes the backend container and releases everything that hangs off it: active mounts and
    /// port mappings are marked inactive, the internal (non-standalone) volume is deactivated and its
    /// docker volume removed. Standalone volumes are kept. Does not save changes.
    /// </summary>
    public async Task ReleaseResourceAsync(ContainerResource resource, DateTime now, CancellationToken cancellationToken)
    {
        ContainerRuntimeNode node = resource.RuntimeNode ?? await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
        if (node.BackendType == (byte)DBContainerBackendType.Docker)
        {
            try { await _backends.Get(node).DeleteContainerAsync(resource.BackendResourceId, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete backend container {ContainerId}", resource.BackendResourceId); }
        }

        resource.DeletedAt = now;
        resource.UpdatedAt = now;

        List<ContainerVolumeMount> mounts = await _db.ContainerVolumeMounts
            .Where(x => x.ContainerResourceId == resource.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (ContainerVolumeMount mount in mounts)
        {
            mount.IsActive = false;
            mount.UnmountedAt = now;
        }

        List<ContainerPortMapping> mappings = await _db.ContainerPortMappings
            .Where(x => x.ContainerResourceId == resource.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (ContainerPortMapping mapping in mappings)
        {
            mapping.IsActive = false;
            mapping.RemovedAt = now;
        }

        ContainerVolume? internalVolume = resource.ContainerVolume
            ?? await _db.ContainerVolumes.FirstOrDefaultAsync(x => x.ContainerResourceId == resource.Id && !x.IsStandalone, cancellationToken);
        if (internalVolume is { IsStandalone: false })
        {
            if (internalVolume.IsActive) await DeleteBackendVolumeAsync(internalVolume, node, cancellationToken);
            internalVolume.IsActive = false;
            internalVolume.DeletedAt ??= now;
            internalVolume.UpdatedAt = now;
        }
    }

    /// <summary>
    /// Stops all running permanent containers of a user (used when the user is disabled) and
    /// expires their temporary containers so the cleanup service removes them. Nothing is deleted here.
    /// </summary>
    public async Task<int> StopUserContainersAsync(int userId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        List<ContainerResource> running = await _db.ContainerResources
            .Include(x => x.RuntimeNode)
            .Where(x => x.OwnerUserId == userId && x.DeletedAt == null && x.StoppedAt == null)
            .ToListAsync(cancellationToken);
        int stopped = 0;
        foreach (ContainerResource resource in running)
        {
            if (!resource.IsPermanent)
            {
                resource.CleanupAt = now;
                resource.UpdatedAt = now;
                continue;
            }
            try
            {
                if (resource.RuntimeNode.BackendType == (byte)DBContainerBackendType.Docker)
                    await _backends.Get(resource.RuntimeNode).StopContainerAsync(resource.BackendResourceId, cancellationToken);
                resource.StoppedAt = now;
                resource.UpdatedAt = now;
                stopped++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop container {ContainerId} of disabled user {UserId}", resource.BackendResourceId, userId);
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return stopped;
    }

    public static ResourceLimits ToLimits(ContainerResource resource) => new()
    {
        CpuCores = resource.CpuCores ?? 0,
        MemoryBytes = resource.MemoryBytes ?? 0,
        MaxProcesses = resource.MaxProcesses ?? 0,
    };

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release();
        }
    }
}
