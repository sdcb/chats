using Chats.BE.Services.WebsiteHosting.Gateways;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.WebsiteHosting;

public sealed record WebsiteReconcileResult(int RoutesApplied, int OrphanRoutesRemoved, int SitesUnpublished, IReadOnlyList<string> Errors);

/// <summary>
/// Brings gateways in line with the database: re-applies routes of active sites (e.g. after a Caddy reload),
/// removes chats-managed routes whose site is no longer active, and unpublishes sites whose container
/// was deleted or whose port mapping disappeared.
/// </summary>
public sealed class WebsiteReconciler(
    ChatsDB db,
    IWebGatewayFactory gatewayFactory,
    ISitePortMapper portMapper,
    WebsitePublishingService publisher,
    ILogger<WebsiteReconciler> logger)
{
    public async Task<WebsiteReconcileResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        int applied = 0, removed = 0, unpublished = 0;
        List<string> errors = [];

        List<PublishedSite> orphaned = await db.PublishedSites
            .Where(x => (x.Status == (byte)DBPublishedSiteStatus.Active || x.Status == (byte)DBPublishedSiteStatus.Failed)
                && x.ContainerResource.DeletedAt != null)
            .ToListAsync(cancellationToken);
        foreach (PublishedSite site in orphaned)
        {
            try
            {
                await publisher.CleanupAsync(site, null, force: true, cancellationToken);
                unpublished++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"site {site.Id}: {ex.Message}");
            }
        }

        List<WebGateway> gateways = await db.WebGateways.AsNoTracking().Where(x => x.IsEnabled).ToListAsync(cancellationToken);
        foreach (WebGateway gatewayEntity in gateways)
        {
            try
            {
                IWebGateway gateway = gatewayFactory.Create(gatewayEntity);
                List<PublishedSite> active = await db.PublishedSites
                    .Where(x => x.Status == (byte)DBPublishedSiteStatus.Active && x.HostedDomain.WebGatewayId == gatewayEntity.Id)
                    .ToListAsync(cancellationToken);
                HashSet<long> activeIds = [.. active.Select(x => x.Id)];

                foreach (PublishedSite site in active)
                {
                    try
                    {
                        string? dial = site.ContainerPortMappingId is long mid ? await portMapper.GetActiveUpstreamAsync(mid, cancellationToken) : null;
                        if (dial == null)
                        {
                            site.LastError = "Port mapping is no longer active.";
                            await publisher.CleanupAsync(site, null, force: true, cancellationToken);
                            activeIds.Remove(site.Id);
                            unpublished++;
                            continue;
                        }
                        await gateway.UpsertSiteRouteAsync(site.Id, site.Hostname, dial, cancellationToken);
                        if (site.UpstreamDial != dial)
                        {
                            site.UpstreamDial = dial;
                            site.UpdatedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(cancellationToken);
                        }
                        applied++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"site {site.Id}: {ex.Message}");
                    }
                }

                foreach (GatewayRouteInfo route in await gateway.ListManagedRoutesAsync(cancellationToken))
                {
                    if (route.SiteId is long sid && activeIds.Contains(sid)) continue;
                    await gateway.DeleteRouteAsync(route.RouteId, cancellationToken);
                    removed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"gateway {gatewayEntity.Name}: {ex.Message}");
            }
        }

        foreach (string error in errors) logger.LogWarning("Website reconcile: {Error}", error);
        return new WebsiteReconcileResult(applied, removed, unpublished, errors);
    }
}

public sealed class WebsiteReconcileService(
    IServiceScopeFactory scopeFactory,
    IOptions<WebsiteHostingOptions> options,
    ILogger<WebsiteReconcileService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WebsiteHostingOptions o = options.Value;
        if (!o.ReconcileEnabled) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, o.ReconcileInitialDelaySeconds)), stoppingToken);
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(Math.Max(30, o.ReconcileIntervalSeconds)));
            do
            {
                try
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
                    if (!await db.PublishedSites.AnyAsync(stoppingToken) && !await db.WebGateways.AnyAsync(x => x.IsEnabled, stoppingToken)) continue;
                    WebsiteReconcileResult r = await scope.ServiceProvider.GetRequiredService<WebsiteReconciler>().ReconcileAsync(stoppingToken);
                    if (r.OrphanRoutesRemoved > 0 || r.SitesUnpublished > 0)
                    {
                        logger.LogInformation("Website reconcile: {Applied} routes applied, {Removed} orphan routes removed, {Unpublished} sites unpublished",
                            r.RoutesApplied, r.OrphanRoutesRemoved, r.SitesUnpublished);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Website reconcile failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
