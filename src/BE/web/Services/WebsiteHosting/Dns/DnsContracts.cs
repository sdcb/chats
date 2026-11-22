namespace Chats.BE.Services.WebsiteHosting.Dns;

/// <summary>An existing record at the provider. <see cref="Name"/> is the fully qualified name.</summary>
public sealed record DnsRecordInfo(string Id, string Type, string Name, string Content, int Ttl, bool? Proxied, string? Comment);

public sealed record DesiredDnsRecord(string Type, string Content);

public sealed record DnsRecordWrite(string Fqdn, string Type, string Content, int Ttl, bool Proxied, string Marker);

public interface IDnsProvider
{
    /// <summary>Verifies credentials (and the zone when given). Returns a short human readable summary.</summary>
    Task<string> TestAsync(string? zone, CancellationToken cancellationToken);

    /// <summary>All records (any type) whose name equals <paramref name="fqdn"/>.</summary>
    Task<IReadOnlyList<DnsRecordInfo>> ListRecordsAsync(string zone, string fqdn, CancellationToken cancellationToken);

    /// <returns>Provider record id.</returns>
    Task<string> CreateRecordAsync(string zone, DnsRecordWrite record, CancellationToken cancellationToken);

    Task UpdateRecordAsync(string zone, string recordId, DnsRecordWrite record, CancellationToken cancellationToken);

    /// <summary>Deletes a record; a record that no longer exists is treated as success.</summary>
    Task DeleteRecordAsync(string zone, string recordId, CancellationToken cancellationToken);
}

public interface IDnsProviderFactory
{
    IDnsProvider Create(Chats.DB.DnsProvider provider);
}

public static class DnsMarker
{
    public const string Prefix = "managed-by:chats";

    public static string For(long siteId) => $"{Prefix} site:{siteId}";

    public static bool IsManaged(string? comment) => comment != null && comment.Trim().StartsWith(Prefix, StringComparison.Ordinal);
}

public enum DnsPlanOpKind
{
    Create,
    Update,
    Keep,
    Delete,
}

public sealed record DnsPlanOp(DnsPlanOpKind Kind, string Type, string Content, string? RecordId);

public sealed record DnsPlan(IReadOnlyList<DnsRecordInfo> Conflicts, IReadOnlyList<DnsPlanOp> Ops)
{
    public bool HasConflicts => Conflicts.Count > 0;
}

public static class DnsRecordPlanner
{
    private static readonly HashSet<string> AddressTypes = new(StringComparer.OrdinalIgnoreCase) { "A", "AAAA", "CNAME" };

    /// <summary>
    /// Computes the changes needed so that exactly <paramref name="desired"/> exist at the name.
    /// Records that would clash (A/AAAA/CNAME, or any type when a CNAME is desired) and are not marked as
    /// managed by Chats are reported as conflicts and nothing is planned. Chats-managed records are reused,
    /// updated or deleted.
    /// </summary>
    public static DnsPlan Plan(IEnumerable<DnsRecordInfo> existing, IReadOnlyList<DesiredDnsRecord> desired, string marker)
    {
        bool wantsCname = desired.Any(x => string.Equals(x.Type, "CNAME", StringComparison.OrdinalIgnoreCase));
        List<DnsRecordInfo> relevant = [.. existing.Where(x => wantsCname || AddressTypes.Contains(x.Type))];
        List<DnsRecordInfo> conflicts = [.. relevant.Where(x => !DnsMarker.IsManaged(x.Comment))];
        if (conflicts.Count > 0) return new DnsPlan(conflicts, []);

        List<DnsRecordInfo> available = [.. relevant];
        List<DnsPlanOp> ops = [];
        foreach (DesiredDnsRecord d in desired)
        {
            DnsRecordInfo? match = available.FirstOrDefault(x => string.Equals(x.Type, d.Type, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                ops.Add(new DnsPlanOp(DnsPlanOpKind.Create, d.Type, d.Content, null));
                continue;
            }
            available.Remove(match);
            bool same = string.Equals(NormalizeContent(match.Content), NormalizeContent(d.Content), StringComparison.OrdinalIgnoreCase)
                && string.Equals(match.Comment?.Trim(), marker, StringComparison.Ordinal);
            ops.Add(new DnsPlanOp(same ? DnsPlanOpKind.Keep : DnsPlanOpKind.Update, d.Type, d.Content, match.Id));
        }
        foreach (DnsRecordInfo leftover in available)
        {
            ops.Add(new DnsPlanOp(DnsPlanOpKind.Delete, leftover.Type, leftover.Content, leftover.Id));
        }
        return new DnsPlan([], ops);
    }

    /// <summary>Desired records for a hosted domain record mode (1=A/AAAA, 2=CNAME).</summary>
    public static IReadOnlyList<DesiredDnsRecord> BuildDesired(byte recordMode, string? publicIpv4, string? publicIpv6, string? cnameTarget)
    {
        List<DesiredDnsRecord> result = [];
        if (recordMode == (byte)Chats.DB.Enums.DBHostedDomainRecordMode.Cname)
        {
            if (string.IsNullOrWhiteSpace(cnameTarget))
                throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayNotConfigured, "The gateway has no CNAME target configured.");
            result.Add(new DesiredDnsRecord("CNAME", cnameTarget.Trim().TrimEnd('.')));
            return result;
        }
        if (!string.IsNullOrWhiteSpace(publicIpv4)) result.Add(new DesiredDnsRecord("A", publicIpv4.Trim()));
        if (!string.IsNullOrWhiteSpace(publicIpv6)) result.Add(new DesiredDnsRecord("AAAA", publicIpv6.Trim()));
        if (result.Count == 0)
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.GatewayNotConfigured, "The gateway has no public IPv4/IPv6 address configured.");
        return result;
    }

    private static string NormalizeContent(string content) => content.Trim().TrimEnd('.');
}
