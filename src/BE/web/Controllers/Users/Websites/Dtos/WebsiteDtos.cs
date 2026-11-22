namespace Chats.BE.Controllers.Users.Websites.Dtos;

public sealed record AvailableDomainDto(int Id, string Domain, string PublicScheme, int PublicPort);

public sealed record WebsiteContainerDto(string Id, string Name, string Image, bool IsRunning);

public sealed record MyWebsiteDto(
    long Id,
    string Hostname,
    string Subdomain,
    string Url,
    string Domain,
    string ContainerId,
    string ContainerName,
    int ContainerPort,
    byte Status,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? UnpublishedAt);

public sealed record PublishWebsiteRequest(string ContainerId, int ContainerPort, int HostedDomainId, string Subdomain);

public sealed record WebsiteEventDto(long Id, string Action, bool IsSuccess, string? Message, DateTime CreatedAt);
