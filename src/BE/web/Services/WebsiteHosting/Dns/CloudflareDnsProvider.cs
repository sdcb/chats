using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.WebsiteHosting.Dns;

/// <summary>Cloudflare DNS via API token (credential JSON: {"apiToken": "..."}; Zone.DNS:Edit + Zone:Read).</summary>
public sealed class CloudflareDnsProvider(HttpClient http, string apiToken) : IDnsProvider
{
    public const string DefaultBaseUrl = "https://api.cloudflare.com/client/v4/";
    private readonly Dictionary<string, string> _zoneIds = new(StringComparer.OrdinalIgnoreCase);

    public static CloudflareDnsProvider FromCredential(HttpClient http, string credentialJson)
    {
        string? token = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(credentialJson);
            if (doc.RootElement.TryGetProperty("apiToken", out JsonElement t)) token = t.GetString();
        }
        catch (JsonException)
        {
            token = credentialJson.Trim();
        }
        if (string.IsNullOrWhiteSpace(token))
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, "Cloudflare credential must be JSON {\"apiToken\":\"...\"}.");
        return new CloudflareDnsProvider(http, token.Trim());
    }

    public async Task<string> TestAsync(string? zone, CancellationToken cancellationToken)
    {
        List<string> parts = [];
        try
        {
            JsonNode? result = await SendAsync(HttpMethod.Get, "user/tokens/verify", null, cancellationToken);
            parts.Add($"token status: {result?["status"]?.GetValue<string>() ?? "unknown"}");
        }
        catch (WebsiteHostingException)
        {
            // Account-owned tokens cannot use /user/tokens/verify; listing zones is authoritative instead.
            parts.Add("token verify endpoint not applicable (account token)");
        }
        if (!string.IsNullOrWhiteSpace(zone))
        {
            string zoneId = await GetZoneIdAsync(zone, cancellationToken);
            parts.Add($"zone {zone} found (id {zoneId})");
        }
        else
        {
            JsonNode? zones = await SendAsync(HttpMethod.Get, "zones?per_page=50", null, cancellationToken);
            parts.Add($"{(zones as JsonArray)?.Count ?? 0} zone(s) visible");
        }
        return string.Join("; ", parts);
    }

    public async Task<IReadOnlyList<DnsRecordInfo>> ListRecordsAsync(string zone, string fqdn, CancellationToken cancellationToken)
    {
        string zoneId = await GetZoneIdAsync(zone, cancellationToken);
        JsonNode? result = await SendAsync(HttpMethod.Get, $"zones/{zoneId}/dns_records?per_page=100&name={Uri.EscapeDataString(fqdn)}", null, cancellationToken);
        List<DnsRecordInfo> records = [];
        if (result is JsonArray arr)
        {
            foreach (JsonNode? r in arr)
            {
                if (r == null) continue;
                records.Add(new DnsRecordInfo(
                    r["id"]!.GetValue<string>(),
                    r["type"]!.GetValue<string>(),
                    r["name"]!.GetValue<string>(),
                    r["content"]?.GetValue<string>() ?? "",
                    r["ttl"]?.GetValue<int>() ?? 1,
                    r["proxied"]?.GetValue<bool>(),
                    r["comment"]?.GetValue<string>()));
            }
        }
        return records;
    }

    public async Task<string> CreateRecordAsync(string zone, DnsRecordWrite record, CancellationToken cancellationToken)
    {
        string zoneId = await GetZoneIdAsync(zone, cancellationToken);
        JsonNode? result = await SendAsync(HttpMethod.Post, $"zones/{zoneId}/dns_records", BuildBody(record), cancellationToken);
        return result?["id"]?.GetValue<string>() ?? throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, "Cloudflare did not return a record id.");
    }

    public async Task UpdateRecordAsync(string zone, string recordId, DnsRecordWrite record, CancellationToken cancellationToken)
    {
        string zoneId = await GetZoneIdAsync(zone, cancellationToken);
        await SendAsync(HttpMethod.Put, $"zones/{zoneId}/dns_records/{Uri.EscapeDataString(recordId)}", BuildBody(record), cancellationToken);
    }

    public async Task DeleteRecordAsync(string zone, string recordId, CancellationToken cancellationToken)
    {
        string zoneId = await GetZoneIdAsync(zone, cancellationToken);
        await SendAsync(HttpMethod.Delete, $"zones/{zoneId}/dns_records/{Uri.EscapeDataString(recordId)}", null, cancellationToken, notFoundOk: true);
    }

    public static JsonObject BuildBody(DnsRecordWrite record) => new()
    {
        ["type"] = record.Type,
        ["name"] = record.Fqdn,
        ["content"] = record.Content,
        ["ttl"] = NormalizeTtl(record.Ttl, record.Proxied),
        ["proxied"] = record.Proxied,
        ["comment"] = record.Marker,
    };

    /// <summary>Cloudflare: proxied records must use automatic TTL (1); otherwise 60..86400 or 1.</summary>
    public static int NormalizeTtl(int ttl, bool proxied)
    {
        if (proxied || ttl <= 1) return 1;
        return Math.Clamp(ttl, 60, 86400);
    }

    private async Task<string> GetZoneIdAsync(string zone, CancellationToken cancellationToken)
    {
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        if (_zoneIds.TryGetValue(z, out string? cached)) return cached;
        JsonNode? result = await SendAsync(HttpMethod.Get, $"zones?name={Uri.EscapeDataString(z)}", null, cancellationToken);
        string? id = (result as JsonArray)?.FirstOrDefault()?["id"]?.GetValue<string>();
        if (id == null) throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, $"Cloudflare zone '{z}' was not found or the token has no access to it.");
        _zoneIds[z] = id;
        return id;
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken, bool notFoundOk = false)
    {
        Uri baseUri = http.BaseAddress ?? new Uri(DefaultBaseUrl);
        using HttpRequestMessage request = new(method, new Uri(baseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        if (body != null) request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, $"Cloudflare request failed: {ex.Message}", ex);
        }
        using (response)
        {
            if (notFoundOk && response.StatusCode == HttpStatusCode.NotFound) return null;
            string text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonNode? root;
            try { root = JsonNode.Parse(text); }
            catch (JsonException) { root = null; }
            bool success = root?["success"]?.GetValue<bool>() == true;
            if (!response.IsSuccessStatusCode || !success)
            {
                string errors = root?["errors"] is JsonArray errs && errs.Count > 0
                    ? string.Join("; ", errs.Select(e => $"{e?["code"]}: {e?["message"]}"))
                    : Truncate(text);
                throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, $"Cloudflare API {method} {path.Split('?')[0]} failed ({(int)response.StatusCode}): {errors}");
            }
            return root?["result"];
        }
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] + "..." : s;
}
