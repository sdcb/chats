using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.WebsiteHosting.Gateways;

public sealed record CaddyAdminCredential(string? Username, string? Password, string? BearerToken)
{
    /// <summary>Optional credential JSON: {"username": "...", "password": "..."} (basic auth) or {"bearerToken": "..."}.</summary>
    public static CaddyAdminCredential? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string? Get(string name) => root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new CaddyAdminCredential(Get("username"), Get("password"), Get("bearerToken"));
        }
        catch (JsonException)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, "Gateway credential must be JSON, e.g. {\"username\":\"...\",\"password\":\"...\"} or {\"bearerToken\":\"...\"}.");
        }
    }
}

public static class CaddyRouteBuilder
{
    public const string RouteIdPrefix = "chats-site-";

    /// <summary>Route ids are unique across the whole Caddy config, so the server name is appended when routes go into several servers.</summary>
    public static string RouteId(long siteId, string server, bool multipleServers)
        => multipleServers ? $"{RouteIdPrefix}{siteId}-{server}" : $"{RouteIdPrefix}{siteId}";

    public static long? TryParseSiteId(string? routeId)
    {
        if (routeId == null || !routeId.StartsWith(RouteIdPrefix, StringComparison.Ordinal)) return null;
        string rest = routeId[RouteIdPrefix.Length..];
        int dash = rest.IndexOf('-');
        if (dash >= 0) rest = rest[..dash];
        return long.TryParse(rest, out long id) ? id : null;
    }

    public static JsonObject BuildRoute(string routeId, string hostname, string upstreamDial) => new()
    {
        ["@id"] = routeId,
        ["match"] = new JsonArray(new JsonObject { ["host"] = new JsonArray(hostname) }),
        ["handle"] = new JsonArray(new JsonObject
        {
            ["handler"] = "reverse_proxy",
            ["upstreams"] = new JsonArray(new JsonObject { ["dial"] = upstreamDial }),
        }),
        ["terminal"] = true,
    };
}

/// <summary>
/// Caddy 2 gateway driven through the Admin API. Routes carry an "@id" so they can be addressed via /id/{routeId};
/// new routes are inserted at index 0 of apps.http.servers.{server}.routes so they take precedence over catch-all routes.
/// Routes added through the API are lost when Caddy reloads its Caddyfile/config; the reconcile service re-applies them.
/// </summary>
public sealed class CaddyWebGateway(HttpClient http, string adminEndpoint, IReadOnlyList<string> serverNames, CaddyAdminCredential? credential) : IWebGateway
{
    private readonly Uri _base = new(adminEndpoint.TrimEnd('/') + "/");

    public static IReadOnlyList<string> ParseServerNames(string? serverNames)
    {
        string[] names = [.. (serverNames ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
        if (names.Length == 0) throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, "Gateway has no server names configured (e.g. srv0).");
        return names;
    }

    public async Task<string> TestAsync(CancellationToken cancellationToken)
    {
        (HttpStatusCode status, string body) = await SendAsync(HttpMethod.Get, "config/", null, cancellationToken);
        EnsureSuccess(status, body, "GET /config/");
        JsonNode? servers = JsonNode.Parse(body)?["apps"]?["http"]?["servers"];
        List<string> available = servers is JsonObject obj ? [.. obj.Select(x => x.Key)] : [];
        List<string> missing = [.. serverNames.Where(x => !available.Contains(x))];
        if (missing.Count > 0)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError,
                $"Caddy is reachable but server(s) {string.Join(", ", missing)} were not found. Available servers: {(available.Count == 0 ? "(none)" : string.Join(", ", available))}.");
        }
        int managed = (await ListManagedRoutesAsync(cancellationToken)).Count;
        return $"Caddy reachable; servers: {string.Join(", ", serverNames)}; chats-managed routes: {managed}";
    }

    public async Task<IReadOnlyList<string>> UpsertSiteRouteAsync(long siteId, string hostname, string upstreamDial, CancellationToken cancellationToken)
    {
        bool multi = serverNames.Count > 1;
        List<string> ids = [];
        foreach (string server in serverNames)
        {
            string routeId = CaddyRouteBuilder.RouteId(siteId, server, multi);
            JsonObject route = CaddyRouteBuilder.BuildRoute(routeId, hostname, upstreamDial);
            (HttpStatusCode existsStatus, string existsBody) = await SendAsync(HttpMethod.Get, $"id/{routeId}", null, cancellationToken);
            if (existsStatus == HttpStatusCode.OK)
            {
                (HttpStatusCode s, string b) = await SendAsync(HttpMethod.Patch, $"id/{routeId}", route, cancellationToken);
                EnsureSuccess(s, b, $"PATCH /id/{routeId}");
            }
            else if (existsStatus == HttpStatusCode.NotFound)
            {
                await InsertRouteAsync(server, route, cancellationToken);
            }
            else
            {
                EnsureSuccess(existsStatus, existsBody, $"GET /id/{routeId}");
            }
            ids.Add(routeId);
        }
        return ids;
    }

    public async Task DeleteRouteAsync(string routeId, CancellationToken cancellationToken)
    {
        (HttpStatusCode status, string body) = await SendAsync(HttpMethod.Delete, $"id/{routeId}", null, cancellationToken);
        if (status == HttpStatusCode.NotFound) return;
        EnsureSuccess(status, body, $"DELETE /id/{routeId}");
    }

    public async Task DeleteSiteRoutesAsync(long siteId, CancellationToken cancellationToken)
    {
        bool multi = serverNames.Count > 1;
        foreach (string server in serverNames)
        {
            await DeleteRouteAsync(CaddyRouteBuilder.RouteId(siteId, server, multi), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<GatewayRouteInfo>> ListManagedRoutesAsync(CancellationToken cancellationToken)
    {
        List<GatewayRouteInfo> result = [];
        foreach (string server in serverNames)
        {
            (HttpStatusCode status, string body) = await SendAsync(HttpMethod.Get, $"config/apps/http/servers/{server}/routes", null, cancellationToken);
            if (status == HttpStatusCode.BadRequest || status == HttpStatusCode.NotFound)
                throw ServerMissing(server, body);
            EnsureSuccess(status, body, $"GET routes of {server}");
            result.AddRange(ParseManagedRoutes(server, body));
        }
        return result;
    }

    public static IEnumerable<GatewayRouteInfo> ParseManagedRoutes(string server, string routesJson)
    {
        if (JsonNode.Parse(routesJson) is not JsonArray routes) yield break;
        foreach (JsonNode? route in routes)
        {
            string? id = route?["@id"]?.GetValue<string>();
            if (id == null || !id.StartsWith(CaddyRouteBuilder.RouteIdPrefix, StringComparison.Ordinal)) continue;
            List<string> hosts = [.. (route!["match"] as JsonArray ?? [])
                .SelectMany(m => m?["host"] as JsonArray ?? [])
                .Select(h => h?.GetValue<string>())
                .OfType<string>()];
            List<string> dials = [.. (route["handle"] as JsonArray ?? [])
                .SelectMany(h => h?["upstreams"] as JsonArray ?? [])
                .Select(u => u?["dial"]?.GetValue<string>())
                .OfType<string>()];
            yield return new GatewayRouteInfo(id, server, CaddyRouteBuilder.TryParseSiteId(id), hosts, dials);
        }
    }

    private async Task InsertRouteAsync(string server, JsonObject route, CancellationToken cancellationToken)
    {
        (HttpStatusCode serverStatus, string serverBody) = await SendAsync(HttpMethod.Get, $"config/apps/http/servers/{server}", null, cancellationToken);
        if (serverStatus != HttpStatusCode.OK || IsJsonNull(serverBody)) throw ServerMissing(server, serverBody);

        (HttpStatusCode routesStatus, string routesBody) = await SendAsync(HttpMethod.Get, $"config/apps/http/servers/{server}/routes", null, cancellationToken);
        EnsureSuccess(routesStatus, routesBody, $"GET routes of {server}");
        if (IsJsonNull(routesBody))
        {
            // The server has no "routes" key yet: create the array.
            (HttpStatusCode s, string b) = await SendAsync(HttpMethod.Put, $"config/apps/http/servers/{server}/routes", new JsonArray(route), cancellationToken);
            EnsureSuccess(s, b, $"PUT routes of {server}");
        }
        else
        {
            // PUT on an array index inserts before that index.
            (HttpStatusCode s, string b) = await SendAsync(HttpMethod.Put, $"config/apps/http/servers/{server}/routes/0", route, cancellationToken);
            EnsureSuccess(s, b, $"PUT routes/0 of {server}");
        }
    }

    private static bool IsJsonNull(string body) => string.IsNullOrWhiteSpace(body) || body.Trim() == "null";

    private static WebsiteHostingException ServerMissing(string server, string body)
        => new(WebsiteHostingErrorCodes.GatewayError,
            $"Caddy HTTP server '{server}' was not found (apps.http.servers.{server}). Check the gateway ServerNames setting. {Truncate(body)}".Trim());

    private static void EnsureSuccess(HttpStatusCode status, string body, string operation)
    {
        if ((int)status is >= 200 and < 300) return;
        throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError, $"Caddy admin API {operation} failed ({(int)status}): {Truncate(body)}");
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] + "..." : s;

    private async Task<(HttpStatusCode, string)> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, new Uri(_base, path));
        if (credential?.BearerToken is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (credential?.Username is { Length: > 0 } user)
        {
            string raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{credential.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError, $"Cannot reach Caddy admin API at {_base}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayError, $"Caddy admin API at {_base} timed out.", ex);
        }
    }
}
