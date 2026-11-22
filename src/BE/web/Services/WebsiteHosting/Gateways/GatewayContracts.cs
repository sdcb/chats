using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.Services.WebsiteHosting.Gateways;

public sealed record GatewayRouteInfo(string RouteId, string Server, long? SiteId, IReadOnlyList<string> Hosts, IReadOnlyList<string> Dials);

public interface IWebGateway
{
    /// <summary>Checks connectivity / configuration. Returns a short human readable summary.</summary>
    Task<string> TestAsync(CancellationToken cancellationToken);

    /// <summary>Creates or replaces the route(s) for a site in every configured server. Returns the route ids.</summary>
    Task<IReadOnlyList<string>> UpsertSiteRouteAsync(long siteId, string hostname, string upstreamDial, CancellationToken cancellationToken);

    /// <summary>Deletes a route by id; a missing route is treated as success.</summary>
    Task DeleteRouteAsync(string routeId, CancellationToken cancellationToken);

    /// <summary>Deletes every route this gateway would create for <paramref name="siteId"/>; missing routes are ignored.</summary>
    Task DeleteSiteRoutesAsync(long siteId, CancellationToken cancellationToken);

    /// <summary>All routes created by Chats (route id prefix <see cref="CaddyRouteBuilder.RouteIdPrefix"/>).</summary>
    Task<IReadOnlyList<GatewayRouteInfo>> ListManagedRoutesAsync(CancellationToken cancellationToken);
}

public interface IWebGatewayFactory
{
    IWebGateway Create(WebGateway gateway);
}

public sealed class WebGatewayFactory(IHttpClientFactory httpClientFactory, WebsiteCredentialProtector protector) : IWebGatewayFactory
{
    public IWebGateway Create(WebGateway gateway) => (DBWebGatewayType)gateway.GatewayType switch
    {
        DBWebGatewayType.Caddy2 => new CaddyWebGateway(
            httpClientFactory.CreateClient(WebsiteHostingHttpClientNames.CaddyAdmin),
            gateway.AdminEndpoint,
            CaddyWebGateway.ParseServerNames(gateway.ServerNames),
            CaddyAdminCredential.Parse(protector.UnprotectNullable(gateway.Credential))),
        _ => throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, $"Unsupported gateway type {gateway.GatewayType}."),
    };
}

public sealed class DnsProviderFactory(IHttpClientFactory httpClientFactory, WebsiteCredentialProtector protector) : IDnsProviderFactory
{
    public IDnsProvider Create(Chats.DB.DnsProvider provider) => (DBDnsProviderType)provider.ProviderType switch
    {
        DBDnsProviderType.Cloudflare => CloudflareDnsProvider.FromCredential(
            httpClientFactory.CreateClient(WebsiteHostingHttpClientNames.Cloudflare), protector.Unprotect(provider.Credential)),
        DBDnsProviderType.AliyunDns => AliyunDnsProvider.FromCredential(
            httpClientFactory.CreateClient(WebsiteHostingHttpClientNames.AliyunDns), protector.Unprotect(provider.Credential)),
        _ => throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, $"Unsupported DNS provider type {provider.ProviderType}."),
    };
}
