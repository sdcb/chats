using System.Net;
using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Containers;

/// <summary>
/// Manages host-port mappings of permanent containers. Host ports are allocated from the
/// runtime node's pool (<see cref="ContainerRuntimeNode.PortRangeStart"/>..<see cref="ContainerRuntimeNode.PortRangeEnd"/>)
/// and applied by recreating the backend container. Not bound to the HTTP user: callers must
/// check ownership themselves. Quota (MaxPortMappings) is evaluated for the container owner; admins bypass it.
/// </summary>
public sealed class ContainerPortMappingService(
    ChatsDB db,
    ContainerRuntimeManager runtime,
    ILogger<ContainerPortMappingService> logger)
{
    public const string ProtocolTcp = "tcp";
    public const string ProtocolUdp = "udp";
    private const int MaxAllocationAttempts = 5;

    private readonly ChatsDB _db = db;
    private readonly ContainerRuntimeManager _runtime = runtime;
    private readonly ILogger<ContainerPortMappingService> _logger = logger;

    public async Task<IReadOnlyList<ContainerPortMapping>> ListAsync(long containerResourceId, CancellationToken cancellationToken)
    {
        return await _db.ContainerPortMappings
            .Include(x => x.RuntimeNode)
            .Where(x => x.ContainerResourceId == containerResourceId && x.IsActive)
            .OrderBy(x => x.ContainerPort)
            .ThenBy(x => x.Protocol)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Idempotently ensures an active mapping for (container, containerPort, protocol) exists and is applied.
    /// Returns the existing mapping unchanged when present (no container recreation).
    /// The returned entity has <see cref="ContainerPortMapping.RuntimeNode"/> loaded.
    /// </summary>
    public async Task<ContainerPortMapping> EnsureMappingAsync(long containerResourceId, int containerPort, string protocol, CancellationToken cancellationToken)
    {
        string proto = NormalizeProtocol(protocol);
        if (containerPort is < 1 or > 65535) throw new ContainerResourceException(ContainerResourceErrorCodes.InvalidPortMapping, "Container port must be between 1 and 65535.");

        using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(containerResourceId, cancellationToken);

        ContainerResource resource = await _db.ContainerResources
            .Include(x => x.RuntimeNode)
            .SingleOrDefaultAsync(x => x.Id == containerResourceId, cancellationToken)
            ?? throw new ContainerResourceException(ContainerResourceErrorCodes.ContainerNotFound, "Container not found.");
        ValidateResource(resource);

        ContainerPortMapping? existing = await _db.ContainerPortMappings
            .Include(x => x.RuntimeNode)
            .FirstOrDefaultAsync(x => x.ContainerResourceId == resource.Id && x.ContainerPort == containerPort && x.Protocol == proto && x.IsActive, cancellationToken);
        if (existing != null) return existing;

        ContainerRuntimeNode node = resource.RuntimeNode;
        if (node.PortRangeStart is not int start || node.PortRangeEnd is not int end || start < 1 || end > 65535 || start > end)
        {
            throw new ContainerResourceException(ContainerResourceErrorCodes.PortPoolNotConfigured, "The runtime node has no host port pool configured.");
        }

        UserContainerQuotum? quota = await ContainerQuotaResolver.ResolveAsync(_db, resource.OwnerUserId, cancellationToken);
        if (quota?.MaxPortMappings is int max)
        {
            int count = await _db.ContainerPortMappings.CountAsync(x => x.IsActive && x.ContainerResource.OwnerUserId == resource.OwnerUserId && x.ContainerResource.DeletedAt == null, cancellationToken);
            if (count >= max) throw new ContainerResourceException(ContainerResourceErrorCodes.QuotaExceeded, "Port mapping quota exceeded.");
        }

        ContainerPortMapping mapping = await AllocateAsync(resource, node, containerPort, proto, start, end, cancellationToken);

        try
        {
            await _runtime.RecreateAsync(resource, cancellationToken);
        }
        catch
        {
            mapping.IsActive = false;
            mapping.RemovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        return mapping;
    }

    /// <summary>Marks a mapping inactive and recreates the container so the binding is released. No-op if already inactive.</summary>
    public async Task RemoveMappingAsync(long mappingId, CancellationToken cancellationToken)
    {
        ContainerPortMapping? mapping = await _db.ContainerPortMappings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mappingId, cancellationToken);
        if (mapping == null) throw new ContainerResourceException(ContainerResourceErrorCodes.NotFound, "Port mapping not found.");
        if (!mapping.IsActive) return;

        using IDisposable _ = await ContainerRuntimeManager.LockResourceAsync(mapping.ContainerResourceId, cancellationToken);
        ContainerPortMapping tracked = await _db.ContainerPortMappings
            .Include(x => x.ContainerResource).ThenInclude(x => x.RuntimeNode)
            .SingleAsync(x => x.Id == mappingId, cancellationToken);
        if (!tracked.IsActive) return;
        tracked.IsActive = false;
        tracked.RemovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        ContainerResource resource = tracked.ContainerResource;
        if (resource.DeletedAt != null || !resource.IsPermanent) return;
        try
        {
            await _runtime.RecreateAsync(resource, cancellationToken);
        }
        catch (ContainerResourceException ex)
        {
            // The mapping stays removed in DB; the stale binding disappears on the next recreation.
            _logger.LogWarning(ex, "Failed to recreate container {ResourceId} after removing port mapping {MappingId}", resource.Id, mappingId);
            throw;
        }
    }

    /// <summary>Upstream dial address ("host:port") a gateway uses to reach the mapping.</summary>
    public static string GetUpstreamAddress(ContainerPortMapping mapping)
        => GetUpstreamAddress(mapping.RuntimeNode ?? throw new InvalidOperationException("RuntimeNode must be loaded."), mapping.HostPort);

    /// <summary>
    /// Upstream dial address: node PublicHost, else the host of a tcp/http(s) node Endpoint, else 127.0.0.1; plus ":" + hostPort.
    /// IPv6 literals are bracketed.
    /// </summary>
    public static string GetUpstreamAddress(ContainerRuntimeNode node, int hostPort)
    {
        string host = GetPublicHost(node);
        if (IPAddress.TryParse(host, out IPAddress? ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) host = $"[{host}]";
        return $"{host}:{hostPort}";
    }

    public static string GetPublicHost(ContainerRuntimeNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.PublicHost)) return node.PublicHost.Trim().Trim('[', ']');
        if (!string.IsNullOrWhiteSpace(node.Endpoint)
            && Uri.TryCreate(node.Endpoint.Trim(), UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "tcp" or "http" or "https"
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host.Trim('[', ']');
        }
        return "127.0.0.1";
    }

    /// <summary>Pure allocator: lowest port in [start, end] not in <paramref name="usedPorts"/>; null when exhausted.</summary>
    public static int? AllocateHostPort(int start, int end, IEnumerable<int> usedPorts)
    {
        HashSet<int> used = [.. usedPorts];
        for (int port = start; port <= end; port++)
        {
            if (!used.Contains(port)) return port;
        }
        return null;
    }

    public static string NormalizeProtocol(string? protocol)
    {
        string proto = string.IsNullOrWhiteSpace(protocol) ? ProtocolTcp : protocol.Trim().ToLowerInvariant();
        if (proto is not (ProtocolTcp or ProtocolUdp)) throw new ContainerResourceException(ContainerResourceErrorCodes.InvalidPortMapping, "Protocol must be tcp or udp.");
        return proto;
    }

    private static void ValidateResource(ContainerResource resource)
    {
        if (resource.DeletedAt != null) throw new ContainerResourceException(ContainerResourceErrorCodes.ContainerDeleted, "Container has been deleted.");
        if (!resource.IsPermanent) throw new ContainerResourceException(ContainerResourceErrorCodes.PermanentContainerRequired, "Port mappings are only supported on permanent containers.");
        string network = resource.BackendNetworkName?.Trim().ToLowerInvariant() ?? "";
        if (network is "none" or "host") throw new ContainerResourceException(ContainerResourceErrorCodes.PortMappingNotSupported, $"Port mappings are not supported with network mode '{resource.BackendNetworkName}'.");
        ContainerRuntimeManager.EnsureDockerNode(resource.RuntimeNode);
    }

    private async Task<ContainerPortMapping> AllocateAsync(ContainerResource resource, ContainerRuntimeNode node, int containerPort, string proto, int start, int end, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            List<int> used = await _db.ContainerPortMappings
                .Where(x => x.RuntimeNodeId == node.Id && x.Protocol == proto && x.IsActive && x.HostPort >= start && x.HostPort <= end)
                .Select(x => x.HostPort)
                .ToListAsync(cancellationToken);
            int hostPort = AllocateHostPort(start, end, used)
                ?? throw new ContainerResourceException(ContainerResourceErrorCodes.PortPoolExhausted, "No free host port is available on the runtime node.");

            ContainerPortMapping mapping = new()
            {
                ContainerResourceId = resource.Id,
                RuntimeNodeId = node.Id,
                RuntimeNode = node,
                ContainerPort = containerPort,
                HostPort = hostPort,
                Protocol = proto,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.ContainerPortMappings.Add(mapping);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return mapping;
            }
            catch (DbUpdateException ex) when (attempt < MaxAllocationAttempts)
            {
                _db.Entry(mapping).State = EntityState.Detached;
                _logger.LogInformation(ex, "Host port {HostPort} allocation raced on node {Node}, retrying ({Attempt})", hostPort, node.Name, attempt);
                ContainerPortMapping? raced = await _db.ContainerPortMappings.Include(x => x.RuntimeNode)
                    .FirstOrDefaultAsync(x => x.ContainerResourceId == resource.Id && x.ContainerPort == containerPort && x.Protocol == proto && x.IsActive, cancellationToken);
                if (raced != null) return raced;
            }
        }
    }
}
