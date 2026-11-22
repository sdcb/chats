using System.Net;
using System.Net.Sockets;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.WebsiteHosting.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.WebsiteHosting;
using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.BE.Services.WebsiteHosting.Gateways;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.WebsiteHosting;

[ApiController]
[AuthorizeAdmin]
[Route("api/admin/website-hosting")]
public sealed class WebsiteHostingAdminController(
    ChatsDB db,
    CurrentUser currentUser,
    WebsiteCredentialProtector protector,
    IWebGatewayFactory gatewayFactory,
    IDnsProviderFactory dnsFactory,
    WebsitePublishingService publisher,
    WebsiteReconciler reconciler) : ControllerBase
{
    // ---------------- gateways ----------------

    [HttpGet("gateways")]
    public async Task<ActionResult<WebGatewayDto[]>> ListGateways(CancellationToken cancellationToken)
    {
        return await db.WebGateways.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new WebGatewayDto(x.Id, x.Name, x.GatewayType, x.AdminEndpoint, x.Credential != null, x.ServerNames,
                x.PublicIpv4, x.PublicIpv6, x.CnameTarget, x.PublicScheme, x.PublicPort, x.IsEnabled, x.HostedDomains.Count, x.CreatedAt, x.UpdatedAt))
            .ToArrayAsync(cancellationToken);
    }

    [HttpPost("gateways")]
    public async Task<IActionResult> CreateGateway([FromBody] UpsertWebGatewayRequest request, CancellationToken cancellationToken)
    {
        WebGateway entity = new() { CreatedAt = DateTime.UtcNow };
        if (ApplyGateway(entity, request) is string error) return BadRequest(error);
        if (await db.WebGateways.AnyAsync(x => x.Name == entity.Name, cancellationToken)) return BadRequest("A gateway with this name already exists.");
        db.WebGateways.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(entity.Id);
    }

    [HttpPut("gateways/{id:int}")]
    public async Task<IActionResult> UpdateGateway(int id, [FromBody] UpsertWebGatewayRequest request, CancellationToken cancellationToken)
    {
        WebGateway? entity = await db.WebGateways.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        if (ApplyGateway(entity, request) is string error) return BadRequest(error);
        if (await db.WebGateways.AnyAsync(x => x.Id != id && x.Name == entity.Name, cancellationToken)) return BadRequest("A gateway with this name already exists.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("gateways/{id:int}")]
    public async Task<IActionResult> DeleteGateway(int id, CancellationToken cancellationToken)
    {
        WebGateway? entity = await db.WebGateways.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        if (await db.HostedDomains.AnyAsync(x => x.WebGatewayId == id, cancellationToken)) return BadRequest("The gateway is used by hosted domains.");
        db.WebGateways.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("gateways/{id:int}/test")]
    public async Task<ActionResult<WebsiteHostingTestResultDto>> TestGateway(int id, CancellationToken cancellationToken)
    {
        WebGateway? entity = await db.WebGateways.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        return await RunTestAsync(() => gatewayFactory.Create(entity).TestAsync(cancellationToken));
    }

    // ---------------- DNS providers ----------------

    [HttpGet("dns-providers")]
    public async Task<ActionResult<DnsProviderDto[]>> ListDnsProviders(CancellationToken cancellationToken)
    {
        return await db.DnsProviders.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new DnsProviderDto(x.Id, x.Name, x.ProviderType, x.Credential != "", x.IsEnabled, x.HostedDomains.Count, x.CreatedAt, x.UpdatedAt))
            .ToArrayAsync(cancellationToken);
    }

    [HttpPost("dns-providers")]
    public async Task<IActionResult> CreateDnsProvider([FromBody] UpsertDnsProviderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Credential)) return BadRequest("Credential is required.");
        DnsProvider entity = new() { CreatedAt = DateTime.UtcNow, Credential = "" };
        if (ApplyDnsProvider(entity, request) is string error) return BadRequest(error);
        if (await db.DnsProviders.AnyAsync(x => x.Name == entity.Name, cancellationToken)) return BadRequest("A DNS provider with this name already exists.");
        db.DnsProviders.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(entity.Id);
    }

    [HttpPut("dns-providers/{id:int}")]
    public async Task<IActionResult> UpdateDnsProvider(int id, [FromBody] UpsertDnsProviderRequest request, CancellationToken cancellationToken)
    {
        DnsProvider? entity = await db.DnsProviders.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Credential) && request.ProviderType != entity.ProviderType)
            return BadRequest("Credential is required when changing the provider type.");
        if (ApplyDnsProvider(entity, request) is string error) return BadRequest(error);
        if (await db.DnsProviders.AnyAsync(x => x.Id != id && x.Name == entity.Name, cancellationToken)) return BadRequest("A DNS provider with this name already exists.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("dns-providers/{id:int}")]
    public async Task<IActionResult> DeleteDnsProvider(int id, CancellationToken cancellationToken)
    {
        DnsProvider? entity = await db.DnsProviders.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        if (await db.HostedDomains.AnyAsync(x => x.DnsProviderId == id, cancellationToken)) return BadRequest("The DNS provider is used by hosted domains.");
        db.DnsProviders.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Verifies the credential; when <paramref name="zone"/> is given also checks access to that zone.</summary>
    [HttpPost("dns-providers/{id:int}/test")]
    public async Task<ActionResult<WebsiteHostingTestResultDto>> TestDnsProvider(int id, [FromQuery] string? zone, CancellationToken cancellationToken)
    {
        DnsProvider? entity = await db.DnsProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        return await RunTestAsync(() => dnsFactory.Create(entity).TestAsync(string.IsNullOrWhiteSpace(zone) ? null : zone.Trim(), cancellationToken));
    }

    // ---------------- hosted domains ----------------

    [HttpGet("domains")]
    public async Task<ActionResult<HostedDomainDto[]>> ListDomains(CancellationToken cancellationToken)
    {
        return await db.HostedDomains.AsNoTracking()
            .OrderBy(x => x.Domain)
            .Select(x => new HostedDomainDto(x.Id, x.Domain, x.DnsZone, x.DnsProviderId, x.DnsProvider.Name, x.WebGatewayId, x.WebGateway.Name,
                x.RecordMode, x.RecordTtl, x.IsProxied, x.AllowAllUsers, x.IsEnabled,
                x.PublishedSites.Count(s => s.Status != (byte)DBPublishedSiteStatus.Unpublished), x.CreatedAt, x.UpdatedAt))
            .ToArrayAsync(cancellationToken);
    }

    [HttpPost("domains")]
    public async Task<IActionResult> CreateDomain([FromBody] UpsertHostedDomainRequest request, CancellationToken cancellationToken)
    {
        HostedDomain entity = new() { CreatedAt = DateTime.UtcNow };
        if (await ApplyDomainAsync(entity, request, cancellationToken) is string error) return BadRequest(error);
        if (await db.HostedDomains.AnyAsync(x => x.Domain == entity.Domain, cancellationToken)) return BadRequest("This domain already exists.");
        db.HostedDomains.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(entity.Id);
    }

    [HttpPut("domains/{id:int}")]
    public async Task<IActionResult> UpdateDomain(int id, [FromBody] UpsertHostedDomainRequest request, CancellationToken cancellationToken)
    {
        HostedDomain? entity = await db.HostedDomains.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        bool hasLiveSites = await db.PublishedSites.AnyAsync(x => x.HostedDomainId == id && x.Status != (byte)DBPublishedSiteStatus.Unpublished, cancellationToken);
        string oldDomain = entity.Domain, oldZone = entity.DnsZone;
        int oldProvider = entity.DnsProviderId, oldGateway = entity.WebGatewayId;
        if (await ApplyDomainAsync(entity, request, cancellationToken) is string error) return BadRequest(error);
        if (hasLiveSites && (entity.Domain != oldDomain || entity.DnsZone != oldZone || entity.DnsProviderId != oldProvider || entity.WebGatewayId != oldGateway))
            return BadRequest("Domain, zone, DNS provider and gateway cannot be changed while websites are published under this domain.");
        if (await db.HostedDomains.AnyAsync(x => x.Id != id && x.Domain == entity.Domain, cancellationToken)) return BadRequest("This domain already exists.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("domains/{id:int}")]
    public async Task<IActionResult> DeleteDomain(int id, CancellationToken cancellationToken)
    {
        HostedDomain? entity = await db.HostedDomains.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();
        if (await db.PublishedSites.AnyAsync(x => x.HostedDomainId == id && x.Status != (byte)DBPublishedSiteStatus.Unpublished, cancellationToken))
            return BadRequest("Unpublish all websites under this domain first.");
        // Only unpublished history remains: drop it together with the domain (events cascade).
        db.PublishedSites.RemoveRange(await db.PublishedSites.Where(x => x.HostedDomainId == id).ToListAsync(cancellationToken));
        db.HostedDomains.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Tests the domain's DNS provider against its zone and its gateway.</summary>
    [HttpPost("domains/{id:int}/test")]
    public async Task<ActionResult<WebsiteHostingTestResultDto>> TestDomain(int id, CancellationToken cancellationToken)
    {
        HostedDomain? entity = await db.HostedDomains.AsNoTracking()
            .Include(x => x.DnsProvider)
            .Include(x => x.WebGateway)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        return await RunTestAsync(async () =>
        {
            string dns = await dnsFactory.Create(entity.DnsProvider).TestAsync(entity.DnsZone, cancellationToken);
            DnsRecordPlanner.BuildDesired(entity.RecordMode, entity.WebGateway.PublicIpv4, entity.WebGateway.PublicIpv6, entity.WebGateway.CnameTarget);
            string gateway = await gatewayFactory.Create(entity.WebGateway).TestAsync(cancellationToken);
            return $"DNS: {dns}\nGateway: {gateway}";
        });
    }

    // ---------------- published sites ----------------

    [HttpGet("sites")]
    public async Task<ActionResult<PublishedSiteAdminDto[]>> ListSites([FromQuery] bool includeUnpublished, [FromQuery] int? userId, CancellationToken cancellationToken)
    {
        IQueryable<PublishedSite> query = db.PublishedSites.AsNoTracking();
        if (!includeUnpublished) query = query.Where(x => x.Status != (byte)DBPublishedSiteStatus.Unpublished);
        if (userId != null) query = query.Where(x => x.UserId == userId);
        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(1000)
            .Select(x => new
            {
                x.Id, x.UserId, x.User.UserName, x.Hostname, x.HostedDomain.Domain, ContainerName = x.ContainerResource.Name,
                ContainerDeleted = x.ContainerResource.DeletedAt != null, x.ContainerPort, x.UpstreamDial, x.Status, x.LastError,
                x.CreatedAt, x.UpdatedAt, x.UnpublishedAt, x.HostedDomain.WebGateway.PublicScheme, x.HostedDomain.WebGateway.PublicPort,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new PublishedSiteAdminDto(x.Id, x.UserId, x.UserName, x.Hostname, SiteNaming.BuildPublicUrl(x.PublicScheme, x.Hostname, x.PublicPort),
            x.Domain, x.ContainerName, x.ContainerDeleted, x.ContainerPort, x.UpstreamDial, x.Status, x.LastError, x.CreatedAt, x.UpdatedAt, x.UnpublishedAt)).ToArray();
    }

    [HttpGet("sites/{id:long}/events")]
    public async Task<ActionResult<PublishedSiteEventDto[]>> ListSiteEvents(long id, CancellationToken cancellationToken)
    {
        return await db.PublishedSiteEvents.AsNoTracking()
            .Where(x => x.PublishedSiteId == id)
            .OrderBy(x => x.Id)
            .Select(x => new PublishedSiteEventDto(x.Id, x.Action, x.IsSuccess, x.Message, x.OperatorUserId, x.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>Unpublishes any site. With <paramref name="force"/> remote cleanup errors are ignored.</summary>
    [HttpPost("sites/{id:long}/unpublish")]
    public async Task<IActionResult> UnpublishSite(long id, [FromQuery] bool force, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.UnpublishAsync(id, currentUser.Id, isAdmin: true, force, cancellationToken);
            return NoContent();
        }
        catch (WebsiteHostingException ex) when (ex.Code == WebsiteHostingErrorCodes.SiteNotFound)
        {
            return NotFound();
        }
        catch (WebsiteHostingException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("reconcile")]
    public async Task<ActionResult<WebsiteReconcileResultDto>> Reconcile(CancellationToken cancellationToken)
    {
        WebsiteReconcileResult r = await reconciler.ReconcileAsync(cancellationToken);
        return new WebsiteReconcileResultDto(r.RoutesApplied, r.OrphanRoutesRemoved, r.SitesUnpublished, r.Errors);
    }

    // ---------------- helpers ----------------

    private static async Task<ActionResult<WebsiteHostingTestResultDto>> RunTestAsync(Func<Task<string>> test)
    {
        try
        {
            return new WebsiteHostingTestResultDto(true, await test());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WebsiteHostingTestResultDto(false, ex.Message);
        }
    }

    private string? ApplyGateway(WebGateway entity, UpsertWebGatewayRequest r)
    {
        string name = (r.Name ?? "").Trim();
        if (name.Length is 0 or > 128) return "Name is required (max 128 characters).";
        if (!Enum.IsDefined((DBWebGatewayType)r.GatewayType)) return "Unsupported gateway type.";
        string endpoint = (r.AdminEndpoint ?? "").Trim();
        if (endpoint.Length > 2048 || !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            return "Admin endpoint must be an absolute http(s) URL, e.g. http://127.0.0.1:2019.";
        string serverNames;
        try { serverNames = string.Join(",", CaddyWebGateway.ParseServerNames(r.ServerNames ?? "")); }
        catch (WebsiteHostingException) { serverNames = ""; }
        if (serverNames.Length is 0 or > 512) return "At least one server name is required (e.g. srv0).";
        string scheme = (r.PublicScheme ?? "").Trim().ToLowerInvariant();
        if (scheme is not ("http" or "https")) return "Public scheme must be http or https.";
        if (r.PublicPort is < 1 or > 65535) return "Public port must be between 1 and 65535.";
        string? ipv4 = Blank(r.PublicIpv4), ipv6 = Blank(r.PublicIpv6), cname = Blank(r.CnameTarget)?.TrimEnd('.').ToLowerInvariant();
        if (ipv4 != null && (!IPAddress.TryParse(ipv4, out IPAddress? a4) || a4.AddressFamily != AddressFamily.InterNetwork)) return "Public IPv4 is invalid.";
        if (ipv6 != null && (!IPAddress.TryParse(ipv6, out IPAddress? a6) || a6.AddressFamily != AddressFamily.InterNetworkV6)) return "Public IPv6 is invalid.";
        if (cname != null && (cname.Length > 253 || !SiteNaming.IsValidHostname(cname))) return "CNAME target must be a valid hostname.";

        string? credential = entity.Credential;
        if (r.ClearCredential) credential = null;
        else if (!string.IsNullOrWhiteSpace(r.Credential))
        {
            try { CaddyAdminCredential.Parse(r.Credential.Trim()); }
            catch (WebsiteHostingException ex) { return ex.Message; }
            credential = protector.Protect(r.Credential.Trim());
        }

        entity.Name = name;
        entity.GatewayType = r.GatewayType;
        entity.AdminEndpoint = endpoint;
        entity.Credential = credential;
        entity.ServerNames = serverNames;
        entity.PublicIpv4 = ipv4;
        entity.PublicIpv6 = ipv6;
        entity.CnameTarget = cname;
        entity.PublicScheme = scheme;
        entity.PublicPort = r.PublicPort;
        entity.IsEnabled = r.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private string? ApplyDnsProvider(DnsProvider entity, UpsertDnsProviderRequest r)
    {
        string name = (r.Name ?? "").Trim();
        if (name.Length is 0 or > 128) return "Name is required (max 128 characters).";
        if (!Enum.IsDefined((DBDnsProviderType)r.ProviderType)) return "Unsupported DNS provider type.";
        string credential = entity.Credential;
        if (!string.IsNullOrWhiteSpace(r.Credential))
        {
            try
            {
                // Parsing validates the credential shape; nothing is sent to the provider.
                dnsFactory.Create(new DnsProvider { ProviderType = r.ProviderType, Credential = r.Credential.Trim(), Name = name });
            }
            catch (WebsiteHostingException ex) { return ex.Message; }
            credential = protector.Protect(r.Credential.Trim());
        }
        entity.Name = name;
        entity.ProviderType = r.ProviderType;
        entity.Credential = credential;
        entity.IsEnabled = r.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> ApplyDomainAsync(HostedDomain entity, UpsertHostedDomainRequest r, CancellationToken cancellationToken)
    {
        string domain = (r.Domain ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        string zone = (r.DnsZone ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        if (domain.Length > 253 || !SiteNaming.IsValidHostname(domain)) return "Domain must be a valid domain name, e.g. sites.example.com.";
        if (zone.Length > 253 || !SiteNaming.IsValidHostname(zone)) return "DNS zone must be a valid domain name, e.g. example.com.";
        if (!SiteNaming.IsDomainInsideZone(domain, zone)) return "Domain must be equal to or inside the DNS zone.";
        if (!Enum.IsDefined((DBHostedDomainRecordMode)r.RecordMode)) return "Unsupported record mode.";
        if (r.RecordTtl < 1) return "TTL must be at least 1.";
        if (!await db.DnsProviders.AnyAsync(x => x.Id == r.DnsProviderId, cancellationToken)) return "DNS provider not found.";
        if (!await db.WebGateways.AnyAsync(x => x.Id == r.WebGatewayId, cancellationToken)) return "Gateway not found.";

        entity.Domain = domain;
        entity.DnsZone = zone;
        entity.DnsProviderId = r.DnsProviderId;
        entity.WebGatewayId = r.WebGatewayId;
        entity.RecordMode = r.RecordMode;
        entity.RecordTtl = r.RecordTtl;
        entity.IsProxied = r.IsProxied;
        entity.AllowAllUsers = r.AllowAllUsers;
        entity.IsEnabled = r.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
