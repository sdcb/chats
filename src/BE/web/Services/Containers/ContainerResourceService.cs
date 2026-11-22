using Chats.BE.Infrastructure;
using Chats.DB;
using Chats.DB.Enums;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Containers;

public sealed class ContainerResourceService(
    ChatsDB db,
    CurrentUser currentUser,
    ContainerBackendFactory backends,
    ContainerRuntimeManager runtime,
    ILogger<ContainerResourceService> logger)
{
    private readonly ChatsDB _db = db;
    private readonly CurrentUser _currentUser = currentUser;
    private readonly ContainerBackendFactory _backends = backends;
    private readonly ContainerRuntimeManager _runtime = runtime;
    private readonly ILogger<ContainerResourceService> _logger = logger;

    public async Task<IReadOnlyList<ContainerResource>> ListMineAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        IQueryable<ContainerResource> query = _db.ContainerResources
            .Include(x => x.RuntimeNode)
            .Include(x => x.ContainerVolume)
            .Include(x => x.ChatContainerResourceAccesses)
            .Where(x => x.OwnerUserId == _currentUser.Id);
        if (!includeDeleted)
        {
            query = query.Where(x => x.DeletedAt == null);
        }
        return await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
    }

    public async Task<ContainerResource> CreateAsync(
        string name,
        bool isPermanent,
        int templateId,
        string? image,
        float? cpuCores,
        long? memoryBytes,
        int? maxProcesses,
        string? networkName,
        int? ownerChatId,
        long? ownerTurnId,
        CancellationToken cancellationToken)
    {
        ContainerResourceTemplate template = await _db.ContainerResourceTemplates
            .Include(x => x.RuntimeNode)
            .SingleOrDefaultAsync(x => x.Id == templateId && x.Visibility != 0, cancellationToken)
            ?? throw new ContainerResourceException("TemplateNotFound", "Container template was not found.");

        if (ownerChatId is not null && !await _db.Chats.AnyAsync(x => x.Id == ownerChatId && x.UserId == _currentUser.Id, cancellationToken))
            throw new UnauthorizedAccessException("Chat does not belong to the current user.");

        ContainerRuntimeNode node = template.RuntimeNode;
        EnsureDockerNode(node);
        IDockerService docker = _backends.Get(node);
        string effectiveImage = string.IsNullOrWhiteSpace(image) ? template.Image : image.Trim();
        string? effectiveNetwork = string.IsNullOrWhiteSpace(networkName) ? template.BackendNetworkName : networkName.Trim();
        UserContainerQuotum? quota = await ResolveQuotaAsync(cancellationToken);
        await ValidateImageAsync(quota, effectiveImage, cancellationToken);
        ValidateNetwork(quota, effectiveNetwork);

        ResourceLimits limits = new()
        {
            CpuCores = cpuCores ?? template.CpuCores,
            MemoryBytes = memoryBytes ?? template.MemoryBytes,
            MaxProcesses = maxProcesses ?? template.MaxProcesses,
        };
        await ValidateQuotaAsync(quota, limits, template.DefaultVolumeBytes, null, cancellationToken);

        DateTime now = DateTime.UtcNow;
        // Permanent containers always get an internal docker volume at the work dir so that data
        // survives container recreation (mount/port-mapping changes). Temporary containers keep the
        // previous behaviour (no backend volume).
        ContainerVolume? internalVolume = null;
        if (isPermanent || template.DefaultVolumeBytes is not null)
        {
            internalVolume = new ContainerVolume
            {
                OwnerUserId = _currentUser.Id,
                RuntimeNodeId = node.Id,
                IsStandalone = false,
                Name = "volume",
                DeclaredBytes = template.DefaultVolumeBytes,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }
        ContainerCreateOptions? options = null;
        if (isPermanent)
        {
            await _runtime.EnsureBackendVolumeAsync(internalVolume!, node, cancellationToken);
            options = ContainerRuntimeManager.BuildCreateOptions(
                new ContainerResource { OwnerUserId = _currentUser.Id, IsPermanent = true },
                docker.Config.WorkDir, internalVolume, [], []);
        }

        ContainerInfo backendContainer;
        try
        {
            backendContainer = await CreateBackendContainerAsync(docker, node, effectiveImage, limits, effectiveNetwork, options, cancellationToken);
        }
        catch
        {
            if (internalVolume?.BackendVolumeId is not null) await _runtime.DeleteBackendVolumeAsync(internalVolume, node, CancellationToken.None);
            throw;
        }
        ContainerResource resource = new()
        {
            OwnerUserId = _currentUser.Id,
            OwnerChatId = ownerChatId,
            OwnerTurnId = ownerTurnId,
            RuntimeNodeId = node.Id,
            IsPermanent = isPermanent,
            BackendResourceId = backendContainer.ContainerId,
            Ip = backendContainer.Ip,
            Name = string.IsNullOrWhiteSpace(name) ? backendContainer.Name : name.Trim(),
            Image = effectiveImage,
            ShellPrefix = backendContainer.ShellPrefix == null ? null : string.Join(',', backendContainer.ShellPrefix),
            CpuCores = limits.CpuCores == 0 ? null : (float)limits.CpuCores,
            MemoryBytes = limits.MemoryBytes == 0 ? null : limits.MemoryBytes,
            MaxProcesses = limits.MaxProcesses == 0 ? null : checked((int)limits.MaxProcesses),
            BackendNetworkName = effectiveNetwork,
            CreatedAt = now,
            UpdatedAt = now,
            LastActiveAt = now,
            CleanupAt = isPermanent ? null : now.AddMinutes(30),
        };
        _db.ContainerResources.Add(resource);
        if (internalVolume is not null)
        {
            internalVolume.Name = $"{resource.Name}-volume";
            resource.ContainerVolume = internalVolume;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return resource;
    }

    public async Task StartAsync(long id, CancellationToken cancellationToken)
    {
        ContainerResource resource = await GetOwnedAsync(id, cancellationToken);
        EnsureUsable(resource);
        ContainerRuntimeNode node = await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
        EnsureDockerNode(node);
        IDockerService docker = _backends.Get(node);
        UserContainerQuotum? quota = await ResolveQuotaAsync(cancellationToken);
        ResourceLimits limits = ToLimits(resource);
        await ValidateQuotaAsync(quota, limits, null, resource.Id, cancellationToken);
        await docker.StartContainerAsync(resource.BackendResourceId, cancellationToken);
        resource.StoppedAt = null;
        resource.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task StopAsync(long id, CancellationToken cancellationToken)
    {
        ContainerResource resource = await GetOwnedAsync(id, cancellationToken);
        EnsureUsable(resource);
        ContainerRuntimeNode node = await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
        EnsureDockerNode(node);
        await _backends.Get(node).StopContainerAsync(resource.BackendResourceId, cancellationToken);
        resource.StoppedAt = DateTime.UtcNow;
        resource.UpdatedAt = resource.StoppedAt.Value;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        ContainerResource resource = await GetOwnedAsync(id, cancellationToken);
        if (resource.DeletedAt is not null) return;
        ContainerRuntimeNode node = await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
        EnsureDockerNode(node);
        using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(resource.Id, cancellationToken);
        await _runtime.ReleaseResourceAsync(resource, DateTime.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(long id, float? cpuCores, long? memoryBytes, int? maxProcesses, string? networkName, CancellationToken cancellationToken)
    {
        ContainerResource resource = await GetOwnedAsync(id, cancellationToken);
        EnsureUsable(resource);
        ResourceLimits next = new()
        {
            CpuCores = cpuCores ?? resource.CpuCores ?? 0,
            MemoryBytes = memoryBytes ?? resource.MemoryBytes ?? 0,
            MaxProcesses = maxProcesses ?? resource.MaxProcesses ?? 0,
        };
        UserContainerQuotum? quota = await ResolveQuotaAsync(cancellationToken);
        await ValidateImageAsync(quota, resource.Image, cancellationToken);
        string? nextNetwork = networkName is null ? resource.BackendNetworkName : networkName.Trim();
        ValidateNetwork(quota, nextNetwork);
        if (!string.Equals(nextNetwork, resource.BackendNetworkName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ContainerResourceException(ContainerResourceErrorCodes.NetworkUpdateNotSupported, "Network mode cannot be changed after container creation.");
        }
        await ValidateQuotaAsync(quota, next, null, resource.Id, cancellationToken);
        try
        {
            ContainerRuntimeNode node = await _db.ContainerRuntimeNodes.SingleAsync(x => x.Id == resource.RuntimeNodeId, cancellationToken);
            EnsureDockerNode(node);
            await _backends.Get(node).UpdateContainerResourcesAsync(resource.BackendResourceId, next, cancellationToken);
        }
        catch
        {
            throw new ContainerResourceException(ContainerResourceErrorCodes.BackendOperationFailed, "Backend resource update failed.");
        }
        resource.CpuCores = next.CpuCores == 0 ? null : (float)next.CpuCores;
        resource.MemoryBytes = next.MemoryBytes == 0 ? null : next.MemoryBytes;
        resource.MaxProcesses = next.MaxProcesses == 0 ? null : checked((int)next.MaxProcesses);
        resource.BackendNetworkName = nextNetwork;
        resource.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantChatAccessAsync(int chatId, long resourceId, CancellationToken cancellationToken)
    {
        ContainerResource resource = await GetOwnedAsync(resourceId, cancellationToken);
        EnsureUsable(resource);
        bool chatOwned = await _db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == _currentUser.Id, cancellationToken);
        if (!chatOwned) throw new UnauthorizedAccessException();
        bool exists = await _db.ChatContainerResourceAccesses.AnyAsync(x => x.ChatId == chatId && x.ContainerResourceId == resourceId, cancellationToken);
        if (!exists)
        {
            _db.ChatContainerResourceAccesses.Add(new ChatContainerResourceAccess { ChatId = chatId, ContainerResourceId = resourceId, GrantedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeChatAccessAsync(int chatId, long resourceId, CancellationToken cancellationToken)
    {
        ChatContainerResourceAccess? access = await _db.ChatContainerResourceAccesses
            .Include(x => x.ContainerResource)
            .SingleOrDefaultAsync(x => x.ChatId == chatId && x.ContainerResourceId == resourceId && x.ContainerResource.OwnerUserId == _currentUser.Id, cancellationToken);
        if (access is null) return;
        _db.ChatContainerResourceAccesses.Remove(access);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ContainerResource> GetOwnedAsync(long id, CancellationToken cancellationToken)
        => await _db.ContainerResources.Include(x => x.ContainerVolume).SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == _currentUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Container resource was not found.");

    private static void EnsureUsable(ContainerResource resource)
    {
        if (resource.DeletedAt is not null) throw new ContainerResourceException(ContainerResourceErrorCodes.ContainerDeleted, "Container has been deleted.");
    }

    private static void EnsureDockerNode(ContainerRuntimeNode node)
    {
        if (!node.IsEnabled) throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeUnavailable, "Runtime node is disabled.");
        if (node.BackendType != (byte)DBContainerBackendType.Docker) throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeNotImplemented, "This runtime backend is not implemented yet.");
    }

    private async Task ValidateImageAsync(UserContainerQuotum? quota, string image, CancellationToken cancellationToken)
    {
        if (quota?.AllowCustomImage == false && !await _db.ContainerImages.AnyAsync(x => x.Image == image && x.IsEnabled, cancellationToken))
        {
            throw new ContainerResourceException(ContainerResourceErrorCodes.ImageNotAllowed, "Image is not enabled in the image catalog.");
        }
    }

    private static void ValidateNetwork(UserContainerQuotum? quota, string? network)
    {
        if (quota is null || string.IsNullOrWhiteSpace(network)) return;
        string policy = quota.AllowedNetworkModes.Trim();
        if (policy == "*") return;
        string[] allowed = policy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (network is null || !allowed.Contains(network, StringComparer.OrdinalIgnoreCase))
            throw new ContainerResourceException(ContainerResourceErrorCodes.NetworkModeNotAllowed, "Requested network is not allowed.");
    }

    private Task<UserContainerQuotum?> ResolveQuotaAsync(CancellationToken cancellationToken)
        => ContainerQuotaResolver.ResolveAsync(_db, _currentUser.Id, cancellationToken);

    private async Task ValidateQuotaAsync(UserContainerQuotum? quota, ResourceLimits limits, long? volumeBytes, long? excludeId, CancellationToken cancellationToken)
    {
        if (quota is null) return;
        if (quota.MaxContainerCpuCores is not null && limits.CpuCores > quota.MaxContainerCpuCores) throw Quota();
        if (quota.MaxContainerMemoryBytes is not null && limits.MemoryBytes > quota.MaxContainerMemoryBytes) throw Quota();
        if (quota.MaxContainerProcesses is not null && limits.MaxProcesses > quota.MaxContainerProcesses) throw Quota();
        if (quota.MaxVolumeBytesPerVolume is not null && volumeBytes is not null && volumeBytes > quota.MaxVolumeBytesPerVolume) throw Quota();
        IQueryable<ContainerResource> resources = _db.ContainerResources.Where(x => x.OwnerUserId == _currentUser.Id && x.DeletedAt == null);
        if (excludeId is not null) resources = resources.Where(x => x.Id != excludeId.Value);
        if (quota.MaxContainerCount is not null && await resources.CountAsync(cancellationToken) + 1 > quota.MaxContainerCount) throw Quota();
        if (quota.MaxCpuCores is not null && await resources.Where(x => x.StoppedAt == null).SumAsync(x => (double?)(x.CpuCores ?? 0), cancellationToken) + limits.CpuCores > quota.MaxCpuCores) throw Quota();
        if (quota.MaxMemoryBytes is not null && await resources.Where(x => x.StoppedAt == null).SumAsync(x => (long?)(x.MemoryBytes ?? 0), cancellationToken) + limits.MemoryBytes > quota.MaxMemoryBytes) throw Quota();
        if (quota.MaxVolumeBytes is not null && volumeBytes is not null && await _db.ContainerVolumes.Where(x => x.OwnerUserId == _currentUser.Id && x.IsActive).SumAsync(x => (long?)(x.DeclaredBytes ?? 0), cancellationToken) + volumeBytes > quota.MaxVolumeBytes) throw Quota();
    }

    private static ContainerResourceException Quota() => new(ContainerResourceErrorCodes.QuotaExceeded, "Container quota exceeded.");

    public async Task<IReadOnlyList<ContainerResourceTemplate>> ListTemplatesAsync(bool forAi, CancellationToken cancellationToken)
    {
        int mask = forAi ? 2 : 1;
        return await _db.ContainerResourceTemplates
            .Include(x => x.RuntimeNode)
            .Where(x => (x.Visibility & mask) != 0)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContainerVolume> CreateStandaloneVolumeAsync(string name, int runtimeNodeId, string? backendVolumeId, long? declaredBytes, CancellationToken cancellationToken)
    {
        if (declaredBytes is < 0) throw new ContainerResourceException(ContainerResourceErrorCodes.InvalidConfiguration, "Declared volume size cannot be negative.");
        if (string.IsNullOrWhiteSpace(name)) throw new ContainerResourceException(ContainerResourceErrorCodes.InvalidConfiguration, "Volume name is required.");
        ContainerRuntimeNode node = await _db.ContainerRuntimeNodes
            .Where(x => x.IsEnabled && (runtimeNodeId == 0 || x.Id == runtimeNodeId))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeUnavailable, "Runtime node is unavailable.");
        EnsureDockerNode(node);
        UserContainerQuotum? quota = await ResolveQuotaAsync(cancellationToken);
        if (quota?.MaxVolumeBytesPerVolume is not null && declaredBytes > quota.MaxVolumeBytesPerVolume) throw Quota();
        if (quota?.MaxVolumeBytes is not null)
        {
            long current = await _db.ContainerVolumes.Where(x => x.OwnerUserId == _currentUser.Id && x.IsActive).SumAsync(x => (long?)(x.DeclaredBytes ?? 0), cancellationToken) ?? 0;
            if (current + (declaredBytes ?? 0) > quota.MaxVolumeBytes) throw Quota();
        }
        DateTime now = DateTime.UtcNow;
        ContainerVolume volume = new()
        {
            OwnerUserId = _currentUser.Id,
            RuntimeNodeId = node.Id,
            IsStandalone = true,
            // Binding an arbitrary existing docker volume is an admin-only capability.
            BackendVolumeId = _currentUser.IsAdmin && !string.IsNullOrWhiteSpace(backendVolumeId) ? backendVolumeId.Trim() : null,
            Name = name.Trim(),
            DeclaredBytes = declaredBytes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _runtime.EnsureBackendVolumeAsync(volume, node, cancellationToken);
        _db.ContainerVolumes.Add(volume);
        await _db.SaveChangesAsync(cancellationToken);
        return volume;
    }

    /// <summary>Mounts an owned standalone volume into an owned permanent container and recreates the container.</summary>
    public async Task<ContainerVolumeMount> MountVolumeAsync(long volumeId, long resourceId, string containerPath, bool isReadOnly, CancellationToken cancellationToken)
    {
        ContainerVolume volume = await _db.ContainerVolumes.SingleOrDefaultAsync(x => x.Id == volumeId && x.OwnerUserId == _currentUser.Id && x.IsStandalone && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Volume was not found.");
        if (!ContainerRuntimeManager.IsValidContainerPath(containerPath))
            throw new ContainerResourceException(ContainerResourceErrorCodes.InvalidMountPath, "Mount path must be an absolute container path.");
        string path = ContainerRuntimeManager.NormalizeContainerPath(containerPath);

        using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(resourceId, cancellationToken);
        ContainerResource resource = await _db.ContainerResources.Include(x => x.RuntimeNode)
            .SingleOrDefaultAsync(x => x.Id == resourceId && x.OwnerUserId == _currentUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Container resource was not found.");
        EnsureUsable(resource);
        if (!resource.IsPermanent) throw new ContainerResourceException(ContainerResourceErrorCodes.PermanentContainerRequired, "Volumes can only be mounted into permanent containers.");
        if (resource.RuntimeNodeId != volume.RuntimeNodeId) throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeMismatch, "Volume and container must be on the same runtime node.");
        EnsureDockerNode(resource.RuntimeNode);

        List<ContainerVolumeMount> active = await _db.ContainerVolumeMounts
            .Where(x => x.ContainerResourceId == resourceId && x.IsActive)
            .ToListAsync(cancellationToken);
        ContainerVolumeMount? same = active.FirstOrDefault(x => x.VolumeId == volumeId && ContainerRuntimeManager.NormalizeContainerPath(x.ContainerPath) == path);
        if (same is not null && same.IsReadOnly == isReadOnly) return same;
        string workDir = ContainerRuntimeManager.NormalizeContainerPath(_backends.Get(resource.RuntimeNode).Config.WorkDir);
        if (path == workDir || active.Any(x => x != same && ContainerRuntimeManager.NormalizeContainerPath(x.ContainerPath) == path))
            throw new ContainerResourceException(ContainerResourceErrorCodes.MountPathConflict, "Another volume is already mounted at this path.");

        DateTime now = DateTime.UtcNow;
        if (same is not null)
        {
            same.IsActive = false;
            same.UnmountedAt = now;
        }
        if (volume.BackendVolumeId is null)
        {
            await _runtime.EnsureBackendVolumeAsync(volume, resource.RuntimeNode, cancellationToken);
            volume.UpdatedAt = now;
        }
        ContainerVolumeMount mount = new()
        {
            VolumeId = volumeId,
            ContainerResourceId = resourceId,
            ContainerPath = path,
            IsReadOnly = isReadOnly,
            IsActive = true,
            MountedAt = now,
        };
        _db.ContainerVolumeMounts.Add(mount);
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await _runtime.RecreateAsync(resource, cancellationToken);
        }
        catch
        {
            mount.IsActive = false;
            mount.UnmountedAt = DateTime.UtcNow;
            if (same is not null)
            {
                same.IsActive = true;
                same.UnmountedAt = null;
            }
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        return mount;
    }

    public async Task UnmountVolumeAsync(long volumeId, long mountId, CancellationToken cancellationToken)
    {
        ContainerVolumeMount probe = await _db.ContainerVolumeMounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == mountId && x.VolumeId == volumeId && x.Volume.OwnerUserId == _currentUser.Id && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Mount was not found.");
        using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(probe.ContainerResourceId, cancellationToken);
        await UnmountCoreAsync(mountId, cancellationToken);
    }

    /// <summary>Deletes an owned standalone volume, unmounting it from any container first.</summary>
    public async Task DeleteStandaloneVolumeAsync(long volumeId, CancellationToken cancellationToken)
    {
        ContainerVolume volume = await _db.ContainerVolumes.Include(x => x.RuntimeNode)
            .SingleOrDefaultAsync(x => x.Id == volumeId && x.OwnerUserId == _currentUser.Id && x.IsStandalone, cancellationToken)
            ?? throw new KeyNotFoundException("Volume was not found.");
        if (!volume.IsActive) return;
        List<ContainerVolumeMount> mounts = await _db.ContainerVolumeMounts.AsNoTracking()
            .Where(x => x.VolumeId == volumeId && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (ContainerVolumeMount mount in mounts)
        {
            using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(mount.ContainerResourceId, cancellationToken);
            await UnmountCoreAsync(mount.Id, cancellationToken);
        }
        DateTime now = DateTime.UtcNow;
        volume.IsActive = false;
        volume.DeletedAt = now;
        volume.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await _runtime.DeleteBackendVolumeAsync(volume, volume.RuntimeNode, CancellationToken.None);
    }

    private async Task UnmountCoreAsync(long mountId, CancellationToken cancellationToken)
    {
        ContainerVolumeMount mount = await _db.ContainerVolumeMounts
            .Include(x => x.ContainerResource).ThenInclude(x => x.RuntimeNode)
            .SingleAsync(x => x.Id == mountId, cancellationToken);
        if (!mount.IsActive) return;
        mount.IsActive = false;
        mount.UnmountedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        ContainerResource resource = mount.ContainerResource;
        if (resource.DeletedAt is null && resource.IsPermanent)
        {
            await _runtime.RecreateAsync(resource, cancellationToken);
        }
    }

    private async Task<ContainerInfo> CreateBackendContainerAsync(IDockerService docker, ContainerRuntimeNode node, string image, ResourceLimits limits, string? network, ContainerCreateOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            return await docker.CreateContainerCoreAsync(image, limits, network, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create container on runtime node {Node}", node.Name);
            throw new ContainerResourceException(ContainerResourceErrorCodes.BackendOperationFailed, "Backend container creation failed.");
        }
    }

    private static ResourceLimits ToLimits(ContainerResource resource) => new()
    {
        CpuCores = resource.CpuCores ?? 0,
        MemoryBytes = resource.MemoryBytes ?? 0,
        MaxProcesses = resource.MaxProcesses ?? 0,
    };
}
