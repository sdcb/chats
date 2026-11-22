using System.Text.Json;
using System.Text.Json.Nodes;
using Chats.BE.Services.Containers;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.WebsiteHosting;

/// <summary>
/// AI tools that let a chat publish ports of its permanent containers as websites.
/// Tools are offered only when the chat owner has at least one available hosted domain and the chat can
/// see at least one running permanent container (owned by the chat or granted to it — the same rule the
/// code interpreter uses). Each call runs in its own DI scope because chat spans execute concurrently.
/// </summary>
public sealed class WebsiteTools(IServiceScopeFactory scopeFactory, ILogger<WebsiteTools> logger)
{
    public const string PublishToolName = "publish_website";
    public const string ListToolName = "list_websites";
    public const string UnpublishToolName = "unpublish_website";

    public static readonly string[] ToolNames = [PublishToolName, ListToolName, UnpublishToolName];

    private static readonly JsonSerializerOptions ResultJsonOptions = new() { WriteIndented = false };

    public static bool IsTool(string name) => ToolNames.Contains(name, StringComparer.Ordinal);

    public static bool IsReadOnlyTool(string name) => name == ListToolName;

    public static void AddTools(IList<ChatTool> tools)
    {
        tools.Add(FunctionTool.Create(PublishToolName,
            "Publish a TCP/HTTP port of a permanent Docker container authorized for this chat as a public website " +
            "(DNS record + reverse proxy). The service inside the container must listen on 0.0.0.0:<container_port> and speak plain HTTP. " +
            "Returns the public URL. Use list_websites to see available domains and existing websites.",
            """
            {"type":"object","properties":{
              "container":{"type":"string","description":"Container name or containerId as listed for this chat."},
              "container_port":{"type":"integer","minimum":1,"maximum":65535,"description":"Port the HTTP service listens on inside the container."},
              "subdomain":{"type":"string","description":"Lowercase DNS label, e.g. 'my-blog' (letters, digits, hyphens)."},
              "domain":{"type":"string","description":"Optional parent domain from list_websites.availableDomains; required when more than one is available."}
            },"required":["container","container_port","subdomain"],"additionalProperties":false}
            """));
        tools.Add(FunctionTool.Create(ListToolName,
            "List websites published from containers authorized for this chat, and the domains available for publishing.",
            """{"type":"object","properties":{},"additionalProperties":false}"""));
        tools.Add(FunctionTool.Create(UnpublishToolName,
            "Unpublish (take offline) a website previously published from a container authorized for this chat. Removes its DNS record and gateway route.",
            """
            {"type":"object","properties":{
              "website":{"type":"string","description":"Hostname (e.g. my-blog.example.com) or website id from list_websites."}
            },"required":["website"],"additionalProperties":false}
            """));
    }

    /// <summary>Whether the website tools should be offered for <paramref name="chatId"/> owned by <paramref name="userId"/>.</summary>
    public async Task<bool> IsAvailableAsync(int userId, int chatId, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            WebsitePublishingService publisher = scope.ServiceProvider.GetRequiredService<WebsitePublishingService>();
            bool isAdmin = await ContainerQuotaResolver.IsAdminAsync(db, userId, cancellationToken);
            if (!await publisher.QueryAvailableDomains(isAdmin).AnyAsync(cancellationToken)) return false;
            return await VisibleContainers(db, userId, chatId).AnyAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to check website tool availability for chat {ChatId}", chatId);
            return false;
        }
    }

    internal static IQueryable<ContainerResource> VisibleContainers(ChatsDB db, int userId, int chatId)
        => db.ContainerResources.Where(x => x.OwnerUserId == userId && x.IsPermanent && x.DeletedAt == null && x.StoppedAt == null &&
            (x.OwnerChatId == chatId || x.ChatContainerResourceAccesses.Any(a => a.ChatId == chatId)));

    public async Task<(bool Success, string Result)> ExecuteAsync(int userId, int chatId, string toolName, string parameters, CancellationToken cancellationToken)
    {
        JsonObject args;
        try
        {
            args = JsonNode.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters) as JsonObject ?? [];
        }
        catch (JsonException ex)
        {
            return (false, $"Invalid JSON arguments: {ex.Message}");
        }

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            WebsitePublishingService publisher = scope.ServiceProvider.GetRequiredService<WebsitePublishingService>();
            bool isAdmin = await ContainerQuotaResolver.IsAdminAsync(db, userId, cancellationToken);
            return toolName switch
            {
                PublishToolName => await PublishAsync(db, publisher, userId, isAdmin, chatId, args, cancellationToken),
                ListToolName => await ListAsync(db, publisher, userId, isAdmin, chatId, cancellationToken),
                UnpublishToolName => await UnpublishAsync(db, publisher, userId, chatId, args, cancellationToken),
                _ => (false, $"Unknown tool: {toolName}"),
            };
        }
        catch (WebsiteHostingException ex)
        {
            return (false, $"{ex.Code}: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Website tool {Tool} failed", toolName);
            return (false, $"Website tool failed: {ex.Message}");
        }
    }

    private static async Task<(bool, string)> PublishAsync(ChatsDB db, WebsitePublishingService publisher, int userId, bool isAdmin, int chatId, JsonObject args, CancellationToken cancellationToken)
    {
        string? containerRef = GetString(args, "container");
        string? subdomain = GetString(args, "subdomain");
        string? domainName = GetString(args, "domain");
        int? port = GetInt(args, "container_port");
        if (string.IsNullOrWhiteSpace(containerRef)) return (false, "Argument 'container' is required.");
        if (string.IsNullOrWhiteSpace(subdomain)) return (false, "Argument 'subdomain' is required.");
        if (port is null) return (false, "Argument 'container_port' must be an integer.");

        List<ContainerResource> containers = await VisibleContainers(db, userId, chatId).AsNoTracking().ToListAsync(cancellationToken);
        ContainerResource? container = ResolveContainer(containers, containerRef);
        if (container == null)
        {
            return (false, containers.Count == 0
                ? "No running permanent container is authorized for this chat."
                : $"Container '{containerRef}' not found. Available: {string.Join(", ", containers.Select(x => x.Name))}");
        }

        List<HostedDomain> domains = await publisher.QueryAvailableDomains(isAdmin).AsNoTracking().OrderBy(x => x.Domain).ToListAsync(cancellationToken);
        HostedDomain? domain;
        if (string.IsNullOrWhiteSpace(domainName))
        {
            if (domains.Count != 1)
                return (false, domains.Count == 0 ? "No domain is available for publishing." : $"Argument 'domain' is required. Available: {string.Join(", ", domains.Select(x => x.Domain))}");
            domain = domains[0];
        }
        else
        {
            string d = domainName.Trim().TrimEnd('.').ToLowerInvariant();
            domain = domains.FirstOrDefault(x => string.Equals(x.Domain, d, StringComparison.OrdinalIgnoreCase));
            if (domain == null) return (false, $"Domain '{domainName}' is not available. Available: {string.Join(", ", domains.Select(x => x.Domain))}");
        }

        PublishedSite site = await publisher.PublishAsync(new PublishSiteCommand(userId, isAdmin, container.Id, port.Value, domain.Id, subdomain, chatId), cancellationToken);
        WebGateway gateway = await db.WebGateways.AsNoTracking().SingleAsync(x => x.Id == domain.WebGatewayId, cancellationToken);
        return (true, JsonSerializer.Serialize(new
        {
            id = site.Id,
            url = WebsitePublishingService.GetPublicUrl(site, gateway),
            hostname = site.Hostname,
            container = container.Name,
            containerPort = site.ContainerPort,
            status = "active",
            note = "DNS propagation may take a few minutes. The service must listen on 0.0.0.0 inside the container.",
        }, ResultJsonOptions));
    }

    private static async Task<(bool, string)> ListAsync(ChatsDB db, WebsitePublishingService publisher, int userId, bool isAdmin, int chatId, CancellationToken cancellationToken)
    {
        IQueryable<ContainerResource> visible = VisibleContainers(db, userId, chatId);
        var sites = await db.PublishedSites.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status != (byte)DBPublishedSiteStatus.Unpublished)
            .Where(x => visible.Any(c => c.Id == x.ContainerResourceId))
            .OrderBy(x => x.Hostname)
            .Select(x => new { x.Id, x.Hostname, x.ContainerPort, x.Status, x.LastError, Container = x.ContainerResource.Name, x.HostedDomain.WebGateway.PublicScheme, x.HostedDomain.WebGateway.PublicPort })
            .ToListAsync(cancellationToken);
        List<string> domains = await publisher.QueryAvailableDomains(isAdmin).OrderBy(x => x.Domain).Select(x => x.Domain).ToListAsync(cancellationToken);
        return (true, JsonSerializer.Serialize(new
        {
            availableDomains = domains,
            websites = sites.Select(x => new
            {
                id = x.Id,
                url = SiteNaming.BuildPublicUrl(x.PublicScheme, x.Hostname, x.PublicPort),
                hostname = x.Hostname,
                container = x.Container,
                containerPort = x.ContainerPort,
                status = ((DBPublishedSiteStatus)x.Status).ToString().ToLowerInvariant(),
                lastError = x.LastError,
            }),
        }, ResultJsonOptions));
    }

    private static async Task<(bool, string)> UnpublishAsync(ChatsDB db, WebsitePublishingService publisher, int userId, int chatId, JsonObject args, CancellationToken cancellationToken)
    {
        string? website = GetString(args, "website")?.Trim().TrimEnd('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(website)) return (false, "Argument 'website' is required.");
        long? id = long.TryParse(website, out long parsed) ? parsed : null;
        string host = website.StartsWith("https://") || website.StartsWith("http://") ? new Uri(website).Host : website;

        IQueryable<ContainerResource> visible = VisibleContainers(db, userId, chatId);
        PublishedSite? site = await db.PublishedSites.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status != (byte)DBPublishedSiteStatus.Unpublished)
            .Where(x => visible.Any(c => c.Id == x.ContainerResourceId))
            .FirstOrDefaultAsync(x => id != null ? x.Id == id : x.Hostname == host, cancellationToken);
        if (site == null) return (false, $"Website '{website}' not found among websites of containers authorized for this chat.");

        await publisher.UnpublishAsync(site.Id, userId, isAdmin: false, force: false, cancellationToken);
        return (true, $"Website {site.Hostname} has been unpublished.");
    }

    internal static ContainerResource? ResolveContainer(IReadOnlyList<ContainerResource> containers, string reference)
    {
        string r = reference.Trim();
        return containers.FirstOrDefault(x => string.Equals(x.Name, r, StringComparison.OrdinalIgnoreCase))
            ?? containers.FirstOrDefault(x => string.Equals(x.BackendResourceId, r, StringComparison.OrdinalIgnoreCase))
            ?? (r.Length >= 12 ? SingleOrNull(containers.Where(x => x.BackendResourceId.StartsWith(r, StringComparison.OrdinalIgnoreCase))) : null);
    }

    private static ContainerResource? SingleOrNull(IEnumerable<ContainerResource> items)
    {
        List<ContainerResource> list = [.. items.Take(2)];
        return list.Count == 1 ? list[0] : null;
    }

    private static string? GetString(JsonObject args, string name)
        => args[name] is JsonValue v && v.TryGetValue(out string? s) ? s : args[name]?.ToString();

    private static int? GetInt(JsonObject args, string name)
    {
        if (args[name] is not JsonValue v) return null;
        if (v.TryGetValue(out int i)) return i;
        if (v.TryGetValue(out long l) && l is >= int.MinValue and <= int.MaxValue) return (int)l;
        if (v.TryGetValue(out double d) && d == Math.Floor(d) && d is >= 1 and <= 65535) return (int)d;
        if (v.TryGetValue(out string? s) && int.TryParse(s, out int p)) return p;
        return null;
    }
}
