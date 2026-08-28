using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Containers;

public sealed class ContainerResourceCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ContainerResourceCleanupService> logger,
    ContainerBackendFactory backends) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ContainerResourceCleanupService> _logger = logger;
    private readonly ContainerBackendFactory _backends = backends;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CleanupOnce(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "ContainerResource cleanup loop failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CleanupOnce(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        DateTime now = DateTime.UtcNow;
        List<ContainerResource> expired = await db.ContainerResources
            .Where(x => !x.IsPermanent && x.DeletedAt == null && x.CleanupAt != null && x.CleanupAt < now)
            .OrderBy(x => x.CleanupAt).Take(50).Include(x => x.RuntimeNode).Include(x => x.ContainerVolume).ToListAsync(cancellationToken);
        foreach (ContainerResource resource in expired)
        {
            try
            {
                if (resource.RuntimeNode.BackendType == 1)
                    await _backends.Get(resource.RuntimeNode).DeleteContainerAsync(resource.BackendResourceId, cancellationToken);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete expired container {ContainerId}", resource.BackendResourceId); }
            resource.DeletedAt = now;
            resource.UpdatedAt = now;
            if (resource.ContainerVolume is { IsStandalone: false } volume)
            {
                volume.IsActive = false;
                volume.DeletedAt = now;
                volume.UpdatedAt = now;
            }
        }
        if (expired.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
