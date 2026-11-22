namespace Chats.BE.Controllers.Admin.WebsiteHosting.Dtos;

public sealed record WebGatewayDto(
    int Id,
    string Name,
    byte GatewayType,
    string AdminEndpoint,
    bool HasCredential,
    string ServerNames,
    string? PublicIpv4,
    string? PublicIpv6,
    string? CnameTarget,
    string PublicScheme,
    int PublicPort,
    bool IsEnabled,
    int DomainCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <param name="Credential">JSON credential; null/empty keeps the stored value unless <paramref name="ClearCredential"/> is set.</param>
public sealed record UpsertWebGatewayRequest(
    string Name,
    byte GatewayType,
    string AdminEndpoint,
    string? Credential,
    bool ClearCredential,
    string ServerNames,
    string? PublicIpv4,
    string? PublicIpv6,
    string? CnameTarget,
    string PublicScheme,
    int PublicPort,
    bool IsEnabled);

public sealed record DnsProviderDto(
    int Id,
    string Name,
    byte ProviderType,
    bool HasCredential,
    bool IsEnabled,
    int DomainCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <param name="Credential">JSON credential: Cloudflare {"apiToken"}, Aliyun {"accessKeyId","accessKeySecret"}. Null/empty keeps the stored value.</param>
public sealed record UpsertDnsProviderRequest(
    string Name,
    byte ProviderType,
    string? Credential,
    bool IsEnabled);

public sealed record HostedDomainDto(
    int Id,
    string Domain,
    string DnsZone,
    int DnsProviderId,
    string DnsProviderName,
    int WebGatewayId,
    string WebGatewayName,
    byte RecordMode,
    int RecordTtl,
    bool IsProxied,
    bool AllowAllUsers,
    bool IsEnabled,
    int LiveSiteCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UpsertHostedDomainRequest(
    string Domain,
    string DnsZone,
    int DnsProviderId,
    int WebGatewayId,
    byte RecordMode,
    int RecordTtl,
    bool IsProxied,
    bool AllowAllUsers,
    bool IsEnabled);

public sealed record WebsiteHostingTestResultDto(bool Success, string Message);

public sealed record PublishedSiteAdminDto(
    long Id,
    int UserId,
    string UserName,
    string Hostname,
    string Url,
    string Domain,
    string ContainerName,
    bool ContainerDeleted,
    int ContainerPort,
    string? UpstreamDial,
    byte Status,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? UnpublishedAt);

public sealed record PublishedSiteEventDto(long Id, string Action, bool IsSuccess, string? Message, int? OperatorUserId, DateTime CreatedAt);

public sealed record WebsiteReconcileResultDto(int RoutesApplied, int OrphanRoutesRemoved, int SitesUnpublished, IReadOnlyList<string> Errors);
