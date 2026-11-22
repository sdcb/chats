using Chats.BE.Services.WebsiteHosting;
using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.BE.Services.WebsiteHosting.Gateways;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.WebsiteHosting;

public sealed class WebsitePublishingServiceTests
{
    private const int OwnerId = 10;
    private const long ContainerId = 100;
    private const int DomainId = 1;

    [Fact]
    public async Task Publish_Success_CreatesDnsAndRoute_AndUnpublishCleansUpInReverseOrder()
    {
        using ChatsDB db = CreateDb();
        Fakes f = new();
        WebsitePublishingService svc = f.Create(db);

        PublishedSite site = await svc.PublishAsync(Command("Blog"), CancellationToken.None);

        Assert.Equal((byte)DBPublishedSiteStatus.Active, site.Status);
        Assert.Equal("blog.sites.example.com", site.Hostname);
        Assert.Equal("10.0.0.2:41000", site.UpstreamDial);
        Assert.Equal(["map-ensure", "dns-list blog.sites.example.com", "dns-create A 203.0.113.10", "route-upsert 1 blog.sites.example.com 10.0.0.2:41000"], f.Log);
        Assert.Equal(DnsMarker.For(site.Id), f.Dns.Records.Single().Comment);
        SiteDnsState dns = WebsitePublishingService.Deserialize<SiteDnsState>(site.DnsRecords)!;
        Assert.Equal("example.com", dns.Zone);
        Assert.Single(dns.Records);
        Assert.Contains(db.PublishedSiteEvents, e => e.Action == "active" && e.Message == "https://blog.sites.example.com");

        f.Log.Clear();
        await svc.UnpublishAsync(site.Id, OwnerId, isAdmin: false, force: false, CancellationToken.None);

        Assert.Equal((byte)DBPublishedSiteStatus.Unpublished, site.Status);
        Assert.NotNull(site.UnpublishedAt);
        Assert.Equal(["route-delete-site 1", "dns-delete rec-1", "map-remove 7"], f.Log);
        Assert.Empty(f.Dns.Records);
    }

    [Fact]
    public async Task Publish_GatewayFailure_RollsBackInReverseOrder_AndMarksFailed()
    {
        using ChatsDB db = CreateDb();
        Fakes f = new() { FailGateway = true };
        WebsitePublishingService svc = f.Create(db);

        WebsiteHostingException ex = await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("blog"), CancellationToken.None));

        Assert.Equal(WebsiteHostingErrorCodes.GatewayError, ex.Code);
        Assert.Equal(
            ["map-ensure", "dns-list blog.sites.example.com", "dns-create A 203.0.113.10", "route-upsert 1 blog.sites.example.com 10.0.0.2:41000",
             "route-delete-site 1", "dns-delete rec-1", "map-remove 7"],
            f.Log);
        PublishedSite site = await db.PublishedSites.SingleAsync();
        Assert.Equal((byte)DBPublishedSiteStatus.Failed, site.Status);
        Assert.Contains("caddy down", site.LastError);
        Assert.Null(site.DnsRecords);
        Assert.Empty(f.Dns.Records);
        Assert.Contains(db.PublishedSiteEvents, e => e.Action == "rollback" && e.IsSuccess);
    }

    [Fact]
    public async Task Publish_DnsConflict_DoesNotTouchForeignRecords_AndKeepsPreexistingMapping()
    {
        using ChatsDB db = CreateDb();
        Fakes f = new() { MappingExisted = true };
        f.Dns.Records.Add(new DnsRecordInfo("foreign", "A", "blog.sites.example.com", "198.51.100.1", 300, null, null));
        WebsitePublishingService svc = f.Create(db);

        WebsiteHostingException ex = await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("blog"), CancellationToken.None));

        Assert.Equal(WebsiteHostingErrorCodes.DnsConflict, ex.Code);
        Assert.Equal(["map-ensure", "dns-list blog.sites.example.com"], f.Log);
        Assert.Single(f.Dns.Records);
        Assert.Equal((byte)DBPublishedSiteStatus.Failed, (await db.PublishedSites.SingleAsync()).Status);
    }

    [Fact]
    public async Task Publish_RetryAfterFailure_ReplacesFailedSite()
    {
        using ChatsDB db = CreateDb();
        Fakes f = new() { FailGateway = true };
        WebsitePublishingService svc = f.Create(db);
        await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("blog"), CancellationToken.None));

        f.FailGateway = false;
        PublishedSite site = await svc.PublishAsync(Command("blog"), CancellationToken.None);

        Assert.Equal((byte)DBPublishedSiteStatus.Active, site.Status);
        Assert.Equal(2, await db.PublishedSites.CountAsync());
        Assert.Equal(1, await db.PublishedSites.CountAsync(x => x.Status == (byte)DBPublishedSiteStatus.Unpublished));
    }

    [Fact]
    public async Task Publish_ValidatesQuotaHostnameAndSubdomain()
    {
        using ChatsDB db = CreateDb();
        Fakes f = new();
        WebsitePublishingService svc = f.Create(db);

        WebsiteHostingException invalid = await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("www"), CancellationToken.None));
        Assert.Equal(WebsiteHostingErrorCodes.InvalidSubdomain, invalid.Code);

        await svc.PublishAsync(Command("one"), CancellationToken.None);
        db.ContainerResources.Add(new ContainerResource
        {
            Id = 101, OwnerUserId = 11, RuntimeNodeId = 1, IsPermanent = true, BackendResourceId = "def", Name = "other", Image = "nginx",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        WebsiteHostingException taken = await Assert.ThrowsAsync<WebsiteHostingException>(
            () => svc.PublishAsync(Command("one") with { UserId = 11, ContainerResourceId = 101 }, CancellationToken.None));
        Assert.Equal(WebsiteHostingErrorCodes.HostnameTaken, taken.Code);

        db.UserContainerQuota.Add(new UserContainerQuotum { UserId = OwnerId, AllowedNetworkModes = "", MaxPublishedSites = 1, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        WebsiteHostingException quota = await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("two"), CancellationToken.None));
        Assert.Equal(WebsiteHostingErrorCodes.QuotaExceeded, quota.Code);
        Assert.DoesNotContain(f.Log, x => x.Contains("two"));
    }

    [Fact]
    public async Task Publish_DomainNotAllowedForNonAdmin()
    {
        using ChatsDB db = CreateDb();
        (await db.HostedDomains.SingleAsync()).AllowAllUsers = false;
        await db.SaveChangesAsync();
        WebsitePublishingService svc = new Fakes().Create(db);

        WebsiteHostingException ex = await Assert.ThrowsAsync<WebsiteHostingException>(() => svc.PublishAsync(Command("blog"), CancellationToken.None));
        Assert.Equal(WebsiteHostingErrorCodes.DomainNotFound, ex.Code);
        PublishedSite site = await svc.PublishAsync(Command("blog") with { IsAdmin = true }, CancellationToken.None);
        Assert.Equal((byte)DBPublishedSiteStatus.Active, site.Status);
    }

    private static PublishSiteCommand Command(string subdomain)
        => new(OwnerId, false, ContainerId, 8080, DomainId, subdomain, null);

    private static ChatsDB CreateDb()
    {
        ChatsDB db = new(new DbContextOptionsBuilder<ChatsDB>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        DateTime now = DateTime.UtcNow;
        db.Users.Add(new User { Id = OwnerId, UserName = "user", DisplayName = "user", Role = "user", Enabled = true, CreatedAt = now, UpdatedAt = now });
        db.Users.Add(new User { Id = 11, UserName = "other", DisplayName = "other", Role = "user", Enabled = true, CreatedAt = now, UpdatedAt = now });
        db.ContainerResources.Add(new ContainerResource
        {
            Id = ContainerId, OwnerUserId = OwnerId, RuntimeNodeId = 1, IsPermanent = true, BackendResourceId = "abc", Name = "web", Image = "nginx",
            CreatedAt = now, UpdatedAt = now,
        });
        db.WebGateways.Add(new WebGateway
        {
            Id = 1, Name = "gw", GatewayType = (byte)DBWebGatewayType.Caddy2, AdminEndpoint = "http://127.0.0.1:2019", ServerNames = "srv0",
            PublicIpv4 = "203.0.113.10", PublicScheme = "https", PublicPort = 443, IsEnabled = true, CreatedAt = now, UpdatedAt = now,
        });
        db.DnsProviders.Add(new DnsProvider { Id = 1, Name = "cf", ProviderType = (byte)DBDnsProviderType.Cloudflare, Credential = "x", IsEnabled = true, CreatedAt = now, UpdatedAt = now });
        db.HostedDomains.Add(new HostedDomain
        {
            Id = DomainId, Domain = "sites.example.com", DnsZone = "example.com", DnsProviderId = 1, WebGatewayId = 1,
            RecordMode = (byte)DBHostedDomainRecordMode.Address, RecordTtl = 300, AllowAllUsers = true, IsEnabled = true, CreatedAt = now, UpdatedAt = now,
        });
        db.SaveChanges();
        return db;
    }

    private sealed class Fakes : ISitePortMapper, IDnsProviderFactory, IWebGatewayFactory
    {
        public List<string> Log { get; } = [];
        public FakeDns Dns { get; }
        public FakeGateway Gateway { get; }
        public bool FailGateway { get; set; }
        public bool MappingExisted { get; init; }

        public Fakes()
        {
            Dns = new FakeDns(Log);
            Gateway = new FakeGateway(this);
        }

        public WebsitePublishingService Create(ChatsDB db)
            => new(db, this, this, this, NullLogger<WebsitePublishingService>.Instance);

        public Task<SitePortMapping> EnsureAsync(long containerResourceId, int containerPort, CancellationToken cancellationToken)
        {
            Log.Add("map-ensure");
            return Task.FromResult(new SitePortMapping(7, "10.0.0.2:41000", !MappingExisted));
        }

        public Task RemoveAsync(long mappingId, CancellationToken cancellationToken)
        {
            Log.Add($"map-remove {mappingId}");
            return Task.CompletedTask;
        }

        public Task<string?> GetActiveUpstreamAsync(long mappingId, CancellationToken cancellationToken) => Task.FromResult<string?>("10.0.0.2:41000");

        IDnsProvider IDnsProviderFactory.Create(DnsProvider provider) => Dns;

        IWebGateway IWebGatewayFactory.Create(WebGateway gateway) => Gateway;
    }

    private sealed class FakeDns(List<string> log) : IDnsProvider
    {
        private int _next;
        public List<DnsRecordInfo> Records { get; } = [];

        public Task<string> TestAsync(string? zone, CancellationToken cancellationToken) => Task.FromResult("ok");

        public Task<IReadOnlyList<DnsRecordInfo>> ListRecordsAsync(string zone, string fqdn, CancellationToken cancellationToken)
        {
            log.Add($"dns-list {fqdn}");
            return Task.FromResult<IReadOnlyList<DnsRecordInfo>>([.. Records.Where(x => x.Name == fqdn)]);
        }

        public Task<string> CreateRecordAsync(string zone, DnsRecordWrite record, CancellationToken cancellationToken)
        {
            string id = $"rec-{++_next}";
            log.Add($"dns-create {record.Type} {record.Content}");
            Records.Add(new DnsRecordInfo(id, record.Type, record.Fqdn, record.Content, record.Ttl, record.Proxied, record.Marker));
            return Task.FromResult(id);
        }

        public Task UpdateRecordAsync(string zone, string recordId, DnsRecordWrite record, CancellationToken cancellationToken)
        {
            log.Add($"dns-update {recordId} {record.Content}");
            int i = Records.FindIndex(x => x.Id == recordId);
            Records[i] = new DnsRecordInfo(recordId, record.Type, record.Fqdn, record.Content, record.Ttl, record.Proxied, record.Marker);
            return Task.CompletedTask;
        }

        public Task DeleteRecordAsync(string zone, string recordId, CancellationToken cancellationToken)
        {
            log.Add($"dns-delete {recordId}");
            Records.RemoveAll(x => x.Id == recordId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGateway(Fakes owner) : IWebGateway
    {
        public Task<string> TestAsync(CancellationToken cancellationToken) => Task.FromResult("ok");

        public Task<IReadOnlyList<string>> UpsertSiteRouteAsync(long siteId, string hostname, string upstreamDial, CancellationToken cancellationToken)
        {
            owner.Log.Add($"route-upsert {siteId} {hostname} {upstreamDial}");
            if (owner.FailGateway) throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError, "caddy down");
            return Task.FromResult<IReadOnlyList<string>>([CaddyRouteBuilder.RouteId(siteId, "srv0", false)]);
        }

        public Task DeleteRouteAsync(string routeId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteSiteRoutesAsync(long siteId, CancellationToken cancellationToken)
        {
            owner.Log.Add($"route-delete-site {siteId}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GatewayRouteInfo>> ListManagedRoutesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GatewayRouteInfo>>([]);
    }
}
