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
    ILogger<ContainerResourceService> logger)
{
    private readonly ChatsDB _db = db;
    private readonly CurrentUser _currentUser = currentUser;
    private readonly ContainerBackendFactory _backends = backends;
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

        ContainerInfo backendContainer = await CreateBackendContainerAsync(docker, node, effectiveImage, limits, effectiveNetwork, cancellationToken);
        DateTime now = DateTime.UtcNow;
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
        if (template.DefaultVolumeBytes is not null)
        {
            resource.ContainerVolume = new ContainerVolume
            {
                OwnerUserId = _currentUser.Id,
                RuntimeNodeId = node.Id,
                IsStandalone = false,
                Name = $"{resource.Name}-volume",
                DeclaredBytes = template.DefaultVolumeBytes,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
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
        try
        {
            await _backends.Get(node).DeleteContainerAsync(resource.BackendResourceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete backend container {ContainerId}; marking resource deleted", resource.BackendResourceId);
        }
        DateTime now = DateTime.UtcNow;
        resource.DeletedAt = now;
        resource.UpdatedAt = now;
        if (resource.ContainerVolume is not null && !resource.ContainerVolume.IsStandalone)
        {
            resource.ContainerVolume.IsActive = false;
            resource.ContainerVolume.DeletedAt = now;
            resource.ContainerVolume.UpdatedAt = now;
        }
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

    private async Task<UserContainerQuotum?> ResolveQuotaAsync(CancellationToken cancellationToken)
        => await _db.UserContainerQuota.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id, cancellationToken)
            ?? await _db.UserContainerQuota.FirstOrDefaultAsync(x => x.UserId == null, cancellationToken);

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
        ContainerRuntimeNode node = await _db.ContainerRuntimeNodes.SingleOrDefaultAsync(x => (runtimeNodeId == 0 ? x.IsEnabled : x.Id == runtimeNodeId && x.IsEnabled), cancellationToken)
            ?? throw new ContainerResourceException(ContainerResourceErrorCodes.RuntimeNodeUnavailable, "Runtime node is unavailable.");
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
            BackendVolumeId = backendVolumeId,
            Name = name.Trim(),
            DeclaredBytes = declaredBytes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.ContainerVolumes.Add(volume);
        await _db.SaveChangesAsync(cancellationToken);
        return volume;
    }

    private async Task<ContainerInfo> CreateBackendContainerAsync(IDockerService docker, ContainerRuntimeNode node, string image, ResourceLimits limits, string? network, CancellationToken cancellationToken)
    {
        try
        {
            return await docker.CreateContainerCoreAsync(image, limits, network, cancellationToken);
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
