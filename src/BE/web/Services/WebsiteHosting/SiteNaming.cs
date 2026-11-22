using System.Text.RegularExpressions;

namespace Chats.BE.Services.WebsiteHosting;

public static partial class SiteNaming
{
    public const int MaxHostnameLength = 253;

    public static readonly IReadOnlySet<string> ReservedSubdomains = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "admin", "administrator", "root", "mail", "email", "smtp", "imap", "pop", "pop3",
        "ftp", "sftp", "ssh", "ns", "ns1", "ns2", "ns3", "ns4", "dns", "mx", "localhost", "autodiscover",
        "autoconfig", "webmail", "cpanel", "whm", "status", "static", "cdn", "assets", "chats", "gateway",
        "caddy", "proxy", "vpn", "git", "login", "auth", "sso", "account", "accounts", "billing", "support",
        "help", "docs", "dashboard", "console", "internal", "intranet",
    };

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex LabelRegex();

    /// <summary>Lowercases/trims the input; when a full hostname under <paramref name="domain"/> is given the label is extracted.</summary>
    public static string Normalize(string? subdomain, string? domain = null)
    {
        string value = (subdomain ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(domain))
        {
            string suffix = "." + domain.Trim().TrimEnd('.').ToLowerInvariant();
            if (value.EndsWith(suffix, StringComparison.Ordinal)) value = value[..^suffix.Length];
        }
        return value;
    }

    /// <returns>null when valid, otherwise a human readable error.</returns>
    public static string? ValidateSubdomain(string subdomain, string domain)
    {
        if (string.IsNullOrEmpty(subdomain)) return "Subdomain is required.";
        if (subdomain.Length > 63) return "Subdomain must be at most 63 characters.";
        if (!LabelRegex().IsMatch(subdomain))
            return "Subdomain must be a single DNS label of lowercase letters, digits and hyphens, and must not start or end with a hyphen.";
        if (subdomain.Length >= 4 && subdomain[2] == '-' && subdomain[3] == '-')
            return "Subdomain must not contain '--' at positions 3-4 (reserved for IDN/punycode).";
        if (ReservedSubdomains.Contains(subdomain)) return $"Subdomain '{subdomain}' is reserved.";
        if (BuildHostname(subdomain, domain).Length > MaxHostnameLength) return "Resulting hostname is too long.";
        return null;
    }

    public static string BuildHostname(string subdomain, string domain)
        => $"{subdomain}.{domain.Trim().TrimEnd('.').ToLowerInvariant()}";

    public static bool IsDefaultPort(string scheme, int port)
        => (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) && port == 443)
           || (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) && port == 80);

    public static string BuildPublicUrl(string scheme, string hostname, int port)
    {
        string s = string.IsNullOrWhiteSpace(scheme) ? "https" : scheme.Trim().ToLowerInvariant();
        return IsDefaultPort(s, port) ? $"{s}://{hostname}" : $"{s}://{hostname}:{port}";
    }

    /// <summary>Relative record name of <paramref name="hostname"/> inside <paramref name="zone"/> ("@" for apex).</summary>
    public static string GetRelativeName(string hostname, string zone)
    {
        string h = hostname.TrimEnd('.').ToLowerInvariant();
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        if (h == z) return "@";
        if (!h.EndsWith("." + z, StringComparison.Ordinal))
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.InvalidConfiguration, $"Hostname '{hostname}' is not inside DNS zone '{zone}'.");
        return h[..^(z.Length + 1)];
    }

    public static bool IsDomainInsideZone(string domain, string zone)
    {
        string d = domain.Trim().TrimEnd('.').ToLowerInvariant();
        string z = zone.Trim().TrimEnd('.').ToLowerInvariant();
        return d == z || d.EndsWith("." + z, StringComparison.Ordinal);
    }

    /// <summary>Lowercase multi-label hostname (at least two labels, each a valid RFC 1123 label).</summary>
    public static bool IsValidHostname(string hostname)
    {
        if (string.IsNullOrEmpty(hostname) || hostname.Length > MaxHostnameLength) return false;
        string[] labels = hostname.Split('.');
        return labels.Length >= 2 && labels.All(l => l.Length <= 63 && LabelRegex().IsMatch(l));
    }
}
