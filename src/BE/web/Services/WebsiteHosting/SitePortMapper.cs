using Chats.BE.Services.Containers;
using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.WebsiteHosting;

public sealed record SitePortMapping(long MappingId, string UpstreamDial, bool Created);

/// <summary>Port mapping operations needed by website publishing (abstracted for testing).</summary>
public interface ISitePortMapper
{
    /// <summary>Ensures a tcp host-port mapping for the container port. <see cref="SitePortMapping.Created"/> is false when it already existed.</summary>
    Task<SitePortMapping> EnsureAsync(long containerResourceId, int containerPort, CancellationToken cancellationToken);

    Task RemoveAsync(long mappingId, CancellationToken cancellationToken);

    /// <summary>Current upstream ("host:port") of an active mapping, or null when the mapping is no longer active.</summary>
    Task<string?> GetActiveUpstreamAsync(long mappingId, CancellationToken cancellationToken);
}

public sealed class ContainerSitePortMapper(ChatsDB db, ContainerPortMappingService portMappings) : ISitePortMapper
{
    public async Task<SitePortMapping> EnsureAsync(long containerResourceId, int containerPort, CancellationToken cancellationToken)
    {
        bool existed = await db.ContainerPortMappings.AnyAsync(x => x.ContainerResourceId == containerResourceId
            && x.ContainerPort == containerPort
            && x.Protocol == ContainerPortMappingService.ProtocolTcp
            && x.IsActive, cancellationToken);
        try
        {
            ContainerPortMapping mapping = await portMappings.EnsureMappingAsync(containerResourceId, containerPort, ContainerPortMappingService.ProtocolTcp, cancellationToken);
            return new SitePortMapping(mapping.Id, ContainerPortMappingService.GetUpstreamAddress(mapping), !existed);
        }
        catch (ContainerResourceException ex)
        {
            throw new WebsiteHostingException(WebsiteHostingErrorCodes.PortMappingFailed, $"Port mapping failed ({ex.Code}): {ex.Message}", ex);
        }
    }

    public async Task RemoveAsync(long mappingId, CancellationToken cancellationToken)
    {
        try
        {
            await portMappings.RemoveMappingAsync(mappingId, cancellationToken);
        }
        catch (ContainerResourceException ex) when (ex.Code == ContainerResourceErrorCodes.NotFound)
        {
        }
    }

    public async Task<string?> GetActiveUpstreamAsync(long mappingId, CancellationToken cancellationToken)
    {
        ContainerPortMapping? mapping = await db.ContainerPortMappings.AsNoTracking()
            .Include(x => x.RuntimeNode)
            .FirstOrDefaultAsync(x => x.Id == mappingId && x.IsActive, cancellationToken);
        return mapping == null ? null : ContainerPortMappingService.GetUpstreamAddress(mapping);
    }
}
