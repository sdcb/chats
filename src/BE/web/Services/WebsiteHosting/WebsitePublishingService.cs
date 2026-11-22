using System.Text.Json;
using System.Text.Json.Serialization;
using Chats.BE.Services.Containers;
using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.BE.Services.WebsiteHosting.Gateways;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.WebsiteHosting;

public sealed record PublishSiteCommand(
    int UserId,
    bool IsAdmin,
    long ContainerResourceId,
    int ContainerPort,
    int HostedDomainId,
    string Subdomain,
    int? ChatId);

/// <summary>JSON persisted in <see cref="PublishedSite.DnsRecords"/>.</summary>
public sealed record SiteDnsState(
    [property: JsonPropertyName("providerId")] int ProviderId,
    [property: JsonPropertyName("zone")] string Zone,
    [property: JsonPropertyName("records")] List<SiteDnsRecordRef> Records);

public sealed record SiteDnsRecordRef(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string Content);

/// <summary>JSON persisted in <see cref="PublishedSite.GatewayRoutes"/>.</summary>
public sealed record SiteGatewayState(
    [property: JsonPropertyName("gatewayId")] int GatewayId,
    [property: JsonPropertyName("routes")] List<string> Routes);

/// <summary>
/// Publishes a permanent container port as a public website: port mapping → DNS records → gateway route.
/// Every step is recorded as a <see cref="PublishedSiteEvent"/>; on failure completed steps are rolled
/// back in reverse order and the site is marked failed.
/// </summary>
public sealed class WebsitePublishingService(
    ChatsDB db,
    ISitePortMapper portMapper,
    IDnsProviderFactory dnsFactory,
    IWebGatewayFactory gatewayFactory,
    ILogger<WebsitePublishingService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly byte[] LiveStatuses =
    [
        (byte)DBPublishedSiteStatus.Publishing,
        (byte)DBPublishedSiteStatus.Active,
        (byte)DBPublishedSiteStatus.Failed,
        (byte)DBPublishedSiteStatus.Unpublishing,
    ];

    /// <summary>Enabled domains (with enabled gateway and DNS provider) the user may publish under.</summary>
    public IQueryable<HostedDomain> QueryAvailableDomains(bool isAdmin)
    {
        return db.HostedDomains
            .Where(x => x.IsEnabled && x.WebGateway.IsEnabled && x.DnsProvider.IsEnabled && (isAdmin || x.AllowAllUsers));
    }

    public static string GetPublicUrl(PublishedSite site, WebGateway gateway)
        => SiteNaming.BuildPublicUrl(gateway.PublicScheme, site.Hostname, gateway.PublicPort);

    public async Task<PublishedSite> PublishAsync(PublishSiteCommand cmd, CancellationToken cancellationToken)
    {
        if (cmd.ContainerPort is < 1 or > 65535)
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidPort, "Container port must be between 1 and 65535.");

        ContainerResource container = await db.ContainerResources.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.ContainerResourceId && x.OwnerUserId == cmd.UserId && x.DeletedAt == null, cancellationToken)
            ?? throw new WebsiteHostingException(WebsiteHostingErrorCodes.ContainerNotFound, "Container not found.");
        if (!container.IsPermanent)
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.PermanentContainerRequired, "Only permanent containers can be published as websites.");

        HostedDomain domain = await QueryAvailableDomains(cmd.IsAdmin)
            .Include(x => x.WebGateway)
            .Include(x => x.DnsProvider)
            .FirstOrDefaultAsync(x => x.Id == cmd.HostedDomainId, cancellationToken)
            ?? throw new WebsiteHostingException(WebsiteHostingErrorCodes.DomainNotFound, "Domain not found or not available.");

        string subdomain = SiteNaming.Normalize(cmd.Subdomain, domain.Domain);
        if (SiteNaming.ValidateSubdomain(subdomain, domain.Domain) is string namingError)
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidSubdomain, namingError);
        string hostname = SiteNaming.BuildHostname(subdomain, domain.Domain);
        if (!SiteNaming.IsDomainInsideZone(hostname, domain.DnsZone))
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, $"Domain '{domain.Domain}' is not inside DNS zone '{domain.DnsZone}'.");
        IReadOnlyList<DesiredDnsRecord> desired = DnsRecordPlanner.BuildDesired(domain.RecordMode, domain.WebGateway.PublicIpv4, domain.WebGateway.PublicIpv6, domain.WebGateway.CnameTarget);

        if (!cmd.IsAdmin)
        {
            UserContainerQuotum? quota = await ContainerQuotaResolver.ResolveAsync(db, cmd.UserId, cancellationToken);
            if (quota?.MaxPublishedSites is int max)
            {
                int count = await db.PublishedSites.CountAsync(x => x.UserId == cmd.UserId && LiveStatuses.Contains(x.Status), cancellationToken);
                if (count >= max)
                    throw new WebsiteHostingException(WebsiteHostingErrorCodes.QuotaExceeded, $"Published website quota exceeded ({max}).");
            }
        }

        PublishedSite? existing = await db.PublishedSites
            .FirstOrDefaultAsync(x => x.Hostname == hostname && LiveStatuses.Contains(x.Status), cancellationToken);
        if (existing != null)
        {
            // A failed site of the same owner is cleaned up and replaced; anything else keeps the hostname.
            if (existing.Status == (byte)DBPublishedSiteStatus.Failed && existing.UserId == cmd.UserId)
            {
                await CleanupAsync(existing, cmd.UserId, force: false, cancellationToken);
            }
            else
            {
                throw new WebsiteHostingException(WebsiteHostingErrorCodes.HostnameTaken, $"Hostname '{hostname}' is already in use.");
            }
        }

        DateTime now = DateTime.UtcNow;
        PublishedSite site = new()
        {
            UserId = cmd.UserId,
            HostedDomainId = domain.Id,
            Subdomain = subdomain,
            Hostname = hostname,
            ContainerResourceId = container.Id,
            ContainerPort = cmd.ContainerPort,
            Status = (byte)DBPublishedSiteStatus.Publishing,
            CreatedByChatId = cmd.ChatId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PublishedSites.Add(site);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            db.Entry(site).State = EntityState.Detached;
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.HostnameTaken, $"Hostname '{hostname}' is already in use.", ex);
        }
        await AddEventAsync(site, "publish", true, $"Publishing {hostname} -> container {container.Name}:{cmd.ContainerPort}", cmd.UserId);

        SitePortMapping? mapping = null;
        SiteDnsState dnsState = new(domain.DnsProviderId, domain.DnsZone, []);
        List<(string id, DnsPlanOpKind kind)> dnsTouched = [];
        bool routeTouched = false;
        IWebGateway gateway = gatewayFactory.Create(domain.WebGateway);
        try
        {
            mapping = await portMapper.EnsureAsync(container.Id, cmd.ContainerPort, cancellationToken);
            site.ContainerPortMappingId = mapping.MappingId;
            site.UpstreamDial = mapping.UpstreamDial;
            await AddEventAsync(site, "port-mapping", true, $"{(mapping.Created ? "Created" : "Reused")} port mapping -> {mapping.UpstreamDial}", cmd.UserId);

            IDnsProvider dns = dnsFactory.Create(domain.DnsProvider);
            IReadOnlyList<DnsRecordInfo> current = await dns.ListRecordsAsync(domain.DnsZone, hostname, cancellationToken);
            string marker = DnsMarker.For(site.Id);
            DnsPlan plan = DnsRecordPlanner.Plan(current, desired, marker);
            if (plan.HasConflicts)
            {
                string list = string.Join(", ", plan.Conflicts.Select(x => $"{x.Type} {x.Content}"));
                throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsConflict,
                    $"DNS records not managed by Chats already exist for {hostname}: {list}. Remove them or choose another subdomain.");
            }
            foreach (DnsPlanOp op in plan.Ops)
            {
                DnsRecordWrite write = new(hostname, op.Type, op.Content, domain.RecordTtl, domain.IsProxied, marker);
                switch (op.Kind)
                {
                    case DnsPlanOpKind.Create:
                        string id = await dns.CreateRecordAsync(domain.DnsZone, write, cancellationToken);
                        dnsTouched.Add((id, op.Kind));
                        dnsState.Records.Add(new SiteDnsRecordRef(id, op.Type, op.Content));
                        break;
                    case DnsPlanOpKind.Update:
                        await dns.UpdateRecordAsync(domain.DnsZone, op.RecordId!, write, cancellationToken);
                        dnsTouched.Add((op.RecordId!, op.Kind));
                        dnsState.Records.Add(new SiteDnsRecordRef(op.RecordId!, op.Type, op.Content));
                        break;
                    case DnsPlanOpKind.Keep:
                        dnsTouched.Add((op.RecordId!, op.Kind));
                        dnsState.Records.Add(new SiteDnsRecordRef(op.RecordId!, op.Type, op.Content));
                        break;
                    case DnsPlanOpKind.Delete:
                        await dns.DeleteRecordAsync(domain.DnsZone, op.RecordId!, cancellationToken);
                        break;
                }
                site.DnsRecords = JsonSerializer.Serialize(dnsState, JsonOptions);
                await AddEventAsync(site, $"dns-{op.Kind.ToString().ToLowerInvariant()}", true, $"{op.Type} {hostname} -> {op.Content}", cmd.UserId);
            }

            routeTouched = true;
            IReadOnlyList<string> routes = await gateway.UpsertSiteRouteAsync(site.Id, hostname, mapping.UpstreamDial, cancellationToken);
            site.GatewayRoutes = JsonSerializer.Serialize(new SiteGatewayState(domain.WebGatewayId, [.. routes]), JsonOptions);
            await AddEventAsync(site, "route-upsert", true, $"Gateway route(s) {string.Join(", ", routes)} -> {mapping.UpstreamDial}", cmd.UserId);

            site.Status = (byte)DBPublishedSiteStatus.Active;
            site.LastError = null;
            await AddEventAsync(site, "active", true, GetPublicUrl(site, domain.WebGateway), cmd.UserId);
            return site;
        }
        catch (Exception ex)
        {
            string error = ex is WebsiteHostingException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            logger.LogWarning(ex, "Publishing site {SiteId} ({Hostname}) failed, rolling back", site.Id, hostname);
            await TryAddEventAsync(site, "publish", false, error, cmd.UserId);

            List<string> rollbackErrors = [];
            if (routeTouched)
            {
                await TryStepAsync(rollbackErrors, "route-delete", () => gateway.DeleteSiteRoutesAsync(site.Id, CancellationToken.None));
            }
            if (dnsTouched.Count > 0)
            {
                IDnsProvider dns = dnsFactory.Create(domain.DnsProvider);
                foreach ((string id, DnsPlanOpKind _) in Enumerable.Reverse(dnsTouched))
                {
                    await TryStepAsync(rollbackErrors, $"dns-delete {id}", () => dns.DeleteRecordAsync(domain.DnsZone, id, CancellationToken.None));
                }
            }
            if (mapping is { Created: true } && !await IsMappingUsedByOtherSiteAsync(mapping.MappingId, site))
            {
                await TryStepAsync(rollbackErrors, "port-mapping-remove", () => portMapper.RemoveAsync(mapping.MappingId, CancellationToken.None));
            }

            site.Status = (byte)DBPublishedSiteStatus.Failed;
            site.LastError = rollbackErrors.Count == 0 ? error : $"{error} (rollback errors: {string.Join("; ", rollbackErrors)})";
            if (rollbackErrors.Count == 0)
            {
                site.DnsRecords = null;
                site.GatewayRoutes = null;
            }
            await TryAddEventAsync(site, "rollback", rollbackErrors.Count == 0, rollbackErrors.Count == 0 ? "Rolled back" : string.Join("; ", rollbackErrors), cmd.UserId);

            if (ex is WebsiteHostingException whe) throw new WebsiteHostingException(whe.Code, site.LastError, ex);
            if (ex is OperationCanceledException) throw;
            throw new WebsiteHostingException(ClassifyError(ex), site.LastError, ex);
        }
    }

    /// <summary>
    /// Unpublishes a site. The operator must own the site unless <paramref name="isAdmin"/>.
    /// With <paramref name="force"/> remote cleanup errors are ignored and the site is marked unpublished anyway.
    /// </summary>
    public async Task<PublishedSite> UnpublishAsync(long siteId, int operatorUserId, bool isAdmin, bool force, CancellationToken cancellationToken)
    {
        PublishedSite site = await db.PublishedSites
            .FirstOrDefaultAsync(x => x.Id == siteId && (isAdmin || x.UserId == operatorUserId), cancellationToken)
            ?? throw new WebsiteHostingException(WebsiteHostingErrorCodes.SiteNotFound, "Website not found.");
        if (site.Status == (byte)DBPublishedSiteStatus.Unpublished) return site;
        await CleanupAsync(site, operatorUserId, force, cancellationToken);
        return site;
    }

    /// <summary>Removes gateway routes, DNS records and the port mapping (if unused) and marks the site unpublished.</summary>
    internal async Task CleanupAsync(PublishedSite site, int? operatorUserId, bool force, CancellationToken cancellationToken)
    {
        site.Status = (byte)DBPublishedSiteStatus.Unpublishing;
        await AddEventAsync(site, "unpublish", true, force ? "Force unpublishing" : "Unpublishing", operatorUserId);

        List<string> errors = [];
        SiteGatewayState? gwState = Deserialize<SiteGatewayState>(site.GatewayRoutes);
        HostedDomain? domain = await db.HostedDomains.AsNoTracking()
            .Include(x => x.WebGateway)
            .Include(x => x.DnsProvider)
            .FirstOrDefaultAsync(x => x.Id == site.HostedDomainId, cancellationToken);

        int? gatewayId = gwState?.GatewayId ?? domain?.WebGatewayId;
        WebGateway? gatewayEntity = gatewayId == null ? null
            : domain?.WebGateway.Id == gatewayId ? domain.WebGateway
            : await db.WebGateways.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gatewayId, cancellationToken);
        if (gatewayEntity != null)
        {
            bool ok = await TryStepAsync(errors, "route-delete", async () =>
            {
                IWebGateway gateway = gatewayFactory.Create(gatewayEntity);
                foreach (string route in gwState?.Routes ?? [])
                {
                    await gateway.DeleteRouteAsync(route, cancellationToken);
                }
                await gateway.DeleteSiteRoutesAsync(site.Id, cancellationToken);
            });
            if (ok)
            {
                site.GatewayRoutes = null;
                await AddEventAsync(site, "route-delete", true, null, operatorUserId);
            }
        }

        SiteDnsState? dnsState = Deserialize<SiteDnsState>(site.DnsRecords);
        if (dnsState != null && dnsState.Records.Count > 0)
        {
            DnsProvider? providerEntity = domain?.DnsProvider.Id == dnsState.ProviderId ? domain.DnsProvider
                : await db.DnsProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dnsState.ProviderId, cancellationToken);
            if (providerEntity == null)
            {
                errors.Add($"dns-delete: DNS provider {dnsState.ProviderId} no longer exists");
            }
            else
            {
                IDnsProvider? dns = null;
                if (await TryStepAsync(errors, "dns-provider", () => { dns = dnsFactory.Create(providerEntity); return Task.CompletedTask; }))
                {
                    foreach (SiteDnsRecordRef record in dnsState.Records.ToList())
                    {
                        bool ok = await TryStepAsync(errors, $"dns-delete {record.Type}", () => dns!.DeleteRecordAsync(dnsState.Zone, record.Id, cancellationToken));
                        if (ok)
                        {
                            dnsState.Records.Remove(record);
                            site.DnsRecords = dnsState.Records.Count == 0 ? null : JsonSerializer.Serialize(dnsState, JsonOptions);
                            await AddEventAsync(site, "dns-delete", true, $"{record.Type} {site.Hostname} -> {record.Content}", operatorUserId);
                        }
                    }
                }
            }
        }

        if (site.ContainerPortMappingId is long mappingId && !await IsMappingUsedByOtherSiteAsync(mappingId, site))
        {
            bool ok = await TryStepAsync(errors, "port-mapping-remove", () => portMapper.RemoveAsync(mappingId, cancellationToken));
            if (ok) await AddEventAsync(site, "port-mapping-remove", true, null, operatorUserId);
        }

        if (errors.Count > 0 && !force)
        {
            site.Status = (byte)DBPublishedSiteStatus.Failed;
            site.LastError = "Unpublish failed: " + string.Join("; ", errors);
            await AddEventAsync(site, "unpublish", false, site.LastError, operatorUserId);
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError, site.LastError);
        }

        site.Status = (byte)DBPublishedSiteStatus.Unpublished;
        site.UnpublishedAt = DateTime.UtcNow;
        site.LastError = errors.Count > 0 ? "Forced unpublish with errors: " + string.Join("; ", errors) : null;
        await AddEventAsync(site, "unpublished", errors.Count == 0, site.LastError, operatorUserId);
    }

    /// <summary>
    /// Removing a port mapping recreates the container, which would kill the services behind other live sites,
    /// so a mapping is kept while any other live site uses it or the same container.
    /// </summary>
    private async Task<bool> IsMappingUsedByOtherSiteAsync(long mappingId, PublishedSite site)
    {
        return await db.PublishedSites.AnyAsync(x => x.Id != site.Id
            && (x.ContainerPortMappingId == mappingId || x.ContainerResourceId == site.ContainerResourceId)
            && LiveStatuses.Contains(x.Status), CancellationToken.None);
    }

    private async Task<bool> TryStepAsync(List<string> errors, string step, Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Website hosting step {Step} failed", step);
            errors.Add($"{step}: {ex.Message}");
            return false;
        }
    }

    private async Task AddEventAsync(PublishedSite site, string action, bool success, string? message, int? operatorUserId)
    {
        DateTime now = DateTime.UtcNow;
        site.UpdatedAt = now;
        db.PublishedSiteEvents.Add(new PublishedSiteEvent
        {
            PublishedSiteId = site.Id,
            Action = action.Length > 64 ? action[..64] : action,
            IsSuccess = success,
            Message = message,
            OperatorUserId = operatorUserId,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task TryAddEventAsync(PublishedSite site, string action, bool success, string? message, int? operatorUserId)
    {
        try
        {
            await AddEventAsync(site, action, success, message, operatorUserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist website event {Action} for site {SiteId}", action, site.Id);
        }
    }

    private static string ClassifyError(Exception ex) => ex switch
    {
        AliyunApiException => WebsiteHostingErrorCodes.DnsError,
        _ => WebsiteHostingErrorCodes.GatewayError,
    };

    public static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
