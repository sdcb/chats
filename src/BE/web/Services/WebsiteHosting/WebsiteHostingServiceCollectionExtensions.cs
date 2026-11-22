using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.BE.Services.WebsiteHosting.Gateways;

namespace Chats.BE.Services.WebsiteHosting;

public static class WebsiteHostingServiceCollectionExtensions
{
    /// <summary>Registers website hosting (#153): gateways, DNS providers, publishing, AI tools and the reconcile loop.</summary>
    public static IServiceCollection AddWebsiteHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebsiteHostingOptions>(configuration.GetSection("WebsiteHosting"));

        // Not traced on purpose: these requests carry gateway / DNS provider credentials.
        foreach (string name in WebsiteHostingHttpClientNames.All)
        {
            services.AddHttpClient(name, http =>
            {
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Sdcb-Chats-WebsiteHosting");
            });
        }

        services.AddSingleton<WebsiteCredentialProtector>();
        services.AddSingleton<IWebGatewayFactory, WebGatewayFactory>();
        services.AddSingleton<IDnsProviderFactory, DnsProviderFactory>();
        services.AddScoped<ISitePortMapper, ContainerSitePortMapper>();
        services.AddScoped<WebsitePublishingService>();
        services.AddScoped<WebsiteReconciler>();
        services.AddSingleton<WebsiteTools>();
        services.AddHostedService<WebsiteReconcileService>();
        return services;
    }
}
