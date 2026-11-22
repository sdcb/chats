namespace Chats.DB.Enums;

/// <summary>Values map to WebGateway.GatewayType.</summary>
public enum DBWebGatewayType : byte
{
    Caddy2 = 1,
}

/// <summary>Values map to DnsProvider.ProviderType.</summary>
public enum DBDnsProviderType : byte
{
    Cloudflare = 1,
    AliyunDns = 2,
}

/// <summary>Values map to HostedDomain.RecordMode.</summary>
public enum DBHostedDomainRecordMode : byte
{
    /// <summary>A/AAAA records pointing to the gateway public IPv4/IPv6.</summary>
    Address = 1,
    /// <summary>CNAME record pointing to the gateway CnameTarget.</summary>
    Cname = 2,
}

/// <summary>Values map to PublishedSite.Status.</summary>
public enum DBPublishedSiteStatus : byte
{
    Publishing = 1,
    Active = 2,
    Failed = 3,
    Unpublishing = 4,
    Unpublished = 5,
}
