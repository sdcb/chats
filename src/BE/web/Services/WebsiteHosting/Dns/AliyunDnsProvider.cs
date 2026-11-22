using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.WebsiteHosting.Dns;

/// <summary>
/// Aliyun (Alibaba Cloud) DNS via the RPC API at alidns.aliyuncs.com, signature v1 (HMAC-SHA1).
/// Credential JSON: {"accessKeyId": "...", "accessKeySecret": "..."}. Chats-managed records carry
/// the marker in the record Remark (set with UpdateDomainRecordRemark).
/// </summary>
public sealed class AliyunDnsProvider(HttpClient http, string accessKeyId, string accessKeySecret, Func<DateTime>? clock = null) : IDnsProvider
{
    public const string DefaultEndpoint = "https://alidns.aliyuncs.com/";
    public const string ApiVersion = "2015-01-09";
    private static readonly HashSet<string> NotFoundCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DomainRecordNotBelongToUser", "InvalidRecordId.NotFound", "InvalidRR.NoExist",
    };

    public static AliyunDnsProvider FromCredential(HttpClient http, string credentialJson)
    {
        string? id = null, secret = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(credentialJson);
            if (doc.RootElement.TryGetProperty("accessKeyId", out JsonElement a)) id = a.GetString();
            if (doc.RootElement.TryGetProperty("accessKeySecret", out JsonElement s)) secret = s.GetString();
        }
        catch (JsonException) { }
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, "Aliyun DNS credential must be JSON {\"accessKeyId\":\"...\",\"accessKeySecret\":\"...\"}.");
        return new AliyunDnsProvider(http, id.Trim(), secret.Trim());
    }

    public async Task<string> TestAsync(string? zone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(zone))
        {
            JsonNode? domains = await CallAsync("DescribeDomains", new() { ["PageSize"] = "1" }, cancellationToken);
            return $"credential valid; {domains?["TotalCount"]?.ToString() ?? "?"} domain(s) visible";
        }
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        JsonNode? info = await CallAsync("DescribeDomainInfo", new() { ["DomainName"] = z }, cancellationToken);
        JsonNode? records = await CallAsync("DescribeDomainRecords", new() { ["DomainName"] = z, ["PageSize"] = "1" }, cancellationToken);
        return $"domain {info?["DomainName"]?.GetValue<string>() ?? z} found ({records?["TotalCount"]?.ToString() ?? "?"} records)";
    }

    public async Task<IReadOnlyList<DnsRecordInfo>> ListRecordsAsync(string zone, string fqdn, CancellationToken cancellationToken)
    {
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        string name = fqdn.Trim().TrimEnd('.').ToLowerInvariant();
        JsonNode? result = await CallAsync("DescribeSubDomainRecords", new()
        {
            ["DomainName"] = z,
            ["SubDomain"] = name,
            ["PageSize"] = "500",
        }, cancellationToken);
        List<DnsRecordInfo> records = [];
        if (result?["DomainRecords"]?["Record"] is JsonArray arr)
        {
            foreach (JsonNode? r in arr)
            {
                if (r == null) continue;
                string rr = r["RR"]?.GetValue<string>() ?? "";
                string recordName = rr == "@" ? z : $"{rr}.{z}";
                if (!string.Equals(recordName, name, StringComparison.OrdinalIgnoreCase)) continue;
                records.Add(new DnsRecordInfo(
                    r["RecordId"]!.ToString(),
                    r["Type"]?.GetValue<string>() ?? "",
                    recordName,
                    r["Value"]?.GetValue<string>() ?? "",
                    r["TTL"] is JsonNode ttl ? (int)ttl.GetValue<long>() : 600,
                    null,
                    DecodeRemark(r["Remark"]?.GetValue<string>())));
            }
        }
        return records;
    }

    public async Task<string> CreateRecordAsync(string zone, DnsRecordWrite record, CancellationToken cancellationToken)
    {
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        JsonNode? result = await CallAsync("AddDomainRecord", new()
        {
            ["DomainName"] = z,
            ["RR"] = SiteNaming.GetRelativeName(record.Fqdn, z),
            ["Type"] = record.Type,
            ["Value"] = record.Content,
            ["TTL"] = Math.Max(1, record.Ttl).ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
        string recordId = result?["RecordId"]?.ToString() ?? throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, "Aliyun DNS did not return a record id.");
        try
        {
            await SetRemarkAsync(recordId, record.Marker, cancellationToken);
        }
        catch
        {
            // Never leave an unmarked record behind: it would later be treated as a foreign record.
            try { await DeleteRecordAsync(z, recordId, CancellationToken.None); } catch { }
            throw;
        }
        return recordId;
    }

    public async Task UpdateRecordAsync(string zone, string recordId, DnsRecordWrite record, CancellationToken cancellationToken)
    {
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        try
        {
            await CallAsync("UpdateDomainRecord", new()
            {
                ["RecordId"] = recordId,
                ["RR"] = SiteNaming.GetRelativeName(record.Fqdn, z),
                ["Type"] = record.Type,
                ["Value"] = record.Content,
                ["TTL"] = Math.Max(1, record.Ttl).ToString(CultureInfo.InvariantCulture),
            }, cancellationToken);
        }
        catch (AliyunApiException ex) when (ex.ApiCode == "DomainRecordDuplicate")
        {
            // Value unchanged.
        }
        await SetRemarkAsync(recordId, record.Marker, cancellationToken);
    }

    public async Task DeleteRecordAsync(string zone, string recordId, CancellationToken cancellationToken)
    {
        try
        {
            await CallAsync("DeleteDomainRecord", new() { ["RecordId"] = recordId }, cancellationToken);
        }
        catch (AliyunApiException ex) when (ex.ApiCode != null && NotFoundCodes.Contains(ex.ApiCode))
        {
        }
    }

    private Task SetRemarkAsync(string recordId, string remark, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(remark)
            ? Task.CompletedTask
            : CallAsync("UpdateDomainRecordRemark", new() { ["RecordId"] = recordId, ["Remark"] = EncodeRemark(remark) }, cancellationToken);

    private const string AliyunMarkerPrefix = "managed-by-chats";

    /// <summary>
    /// AliDNS remarks only allow letters, digits, CJK and "_ - , ." (max 50 chars), so the marker
    /// "managed-by:chats site:1" is stored as "managed-by-chats.site-1" and mapped back when listing.
    /// </summary>
    public static string EncodeRemark(string marker)
    {
        string s = marker.Replace(DnsMarker.Prefix, AliyunMarkerPrefix, StringComparison.Ordinal).Replace(" site:", ".site-", StringComparison.Ordinal);
        return s.Length > 50 ? s[..50] : s;
    }

    public static string? DecodeRemark(string? remark)
    {
        if (remark == null || !remark.StartsWith(AliyunMarkerPrefix, StringComparison.Ordinal)) return remark;
        return DnsMarker.Prefix + remark[AliyunMarkerPrefix.Length..].Replace(".site-", " site:", StringComparison.Ordinal);
    }

    private async Task<JsonNode?> CallAsync(string action, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        SortedDictionary<string, string> all = new(StringComparer.Ordinal)
        {
            ["Action"] = action,
            ["Format"] = "JSON",
            ["Version"] = ApiVersion,
            ["AccessKeyId"] = accessKeyId,
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureVersion"] = "1.0",
            ["SignatureNonce"] = Guid.NewGuid().ToString("N"),
            ["Timestamp"] = (clock?.Invoke() ?? DateTime.UtcNow).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };
        foreach (KeyValuePair<string, string> p in parameters) all[p.Key] = p.Value;
        string signature = AliyunRpcSigner.ComputeSignature(all, accessKeySecret, "GET");
        string query = AliyunRpcSigner.BuildCanonicalQuery(all) + "&Signature=" + AliyunRpcSigner.PercentEncode(signature);

        Uri baseUri = http.BaseAddress ?? new Uri(DefaultEndpoint);
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(new Uri(baseUri, "?" + query), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.DnsError, $"Aliyun DNS request failed: {ex.Message}", ex);
        }
        using (response)
        {
            string text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonNode? root;
            try { root = JsonNode.Parse(text); }
            catch (JsonException) { root = null; }
            if (!response.IsSuccessStatusCode)
            {
                string? code = root?["Code"]?.GetValue<string>();
                string message = root?["Message"]?.GetValue<string>() ?? (text.Length > 300 ? text[..300] : text);
                throw new AliyunApiException(code, $"Aliyun DNS {action} failed ({(int)response.StatusCode} {code}): {message}");
            }
            return root;
        }
    }
}

public sealed class AliyunApiException(string? apiCode, string message) : Exception(message)
{
    public string? ApiCode { get; } = apiCode;
}

public static class AliyunRpcSigner
{
    /// <summary>RFC 3986 percent-encoding as required by Aliyun RPC signatures (space→%20, '*'→%2A, '~' kept).</summary>
    public static string PercentEncode(string value)
    {
        StringBuilder sb = new();
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            char c = (char)b;
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.' or '~') sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static string BuildCanonicalQuery(IEnumerable<KeyValuePair<string, string>> parameters)
        => string.Join("&", parameters
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{PercentEncode(x.Key)}={PercentEncode(x.Value)}"));

    public static string BuildStringToSign(IEnumerable<KeyValuePair<string, string>> parameters, string method = "GET")
        => $"{method}&{PercentEncode("/")}&{PercentEncode(BuildCanonicalQuery(parameters))}";

    public static string ComputeSignature(IEnumerable<KeyValuePair<string, string>> parameters, string accessKeySecret, string method = "GET")
    {
        byte[] hash = HMACSHA1.HashData(Encoding.UTF8.GetBytes(accessKeySecret + "&"), Encoding.UTF8.GetBytes(BuildStringToSign(parameters, method)));
        return Convert.ToBase64String(hash);
    }
}
