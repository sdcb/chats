using Chats.BE.Controllers.Users.Containers.Dtos;
using Chats.BE.Controllers.Users.Websites.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services.WebsiteHosting;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Users.Websites;

[ApiController]
[Authorize]
[Route("api/websites")]
public sealed class WebsiteController(
    ChatsDB db,
    CurrentUser currentUser,
    WebsitePublishingService publisher,
    IUrlEncryptionService encryption) : ControllerBase
{
    [HttpGet("domains")]
    public async Task<ActionResult<AvailableDomainDto[]>> Domains(CancellationToken cancellationToken)
    {
        return await publisher.QueryAvailableDomains(currentUser.IsAdmin)
            .OrderBy(x => x.Domain)
            .Select(x => new AvailableDomainDto(x.Id, x.Domain, x.WebGateway.PublicScheme, x.WebGateway.PublicPort))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>My permanent containers that can be published.</summary>
    [HttpGet("containers")]
    public async Task<ActionResult<WebsiteContainerDto[]>> Containers(CancellationToken cancellationToken)
    {
        var rows = await db.ContainerResources.AsNoTracking()
            .Where(x => x.OwnerUserId == currentUser.Id && x.IsPermanent && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Image, x.StoppedAt })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new WebsiteContainerDto(EncryptContainerId(x.Id), x.Name, x.Image, x.StoppedAt == null)).ToArray();
    }

    [HttpGet]
    public async Task<ActionResult<MyWebsiteDto[]>> List([FromQuery] bool includeUnpublished, CancellationToken cancellationToken)
    {
        IQueryable<PublishedSite> query = db.PublishedSites.AsNoTracking().Where(x => x.UserId == currentUser.Id);
        if (!includeUnpublished) query = query.Where(x => x.Status != (byte)DBPublishedSiteStatus.Unpublished);
        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(500)
            .Select(x => new
            {
                x.Id, x.Hostname, x.Subdomain, x.HostedDomain.Domain, x.ContainerResourceId, ContainerName = x.ContainerResource.Name, x.ContainerPort,
                x.Status, x.LastError, x.CreatedAt, x.UpdatedAt, x.UnpublishedAt, x.HostedDomain.WebGateway.PublicScheme, x.HostedDomain.WebGateway.PublicPort,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new MyWebsiteDto(x.Id, x.Hostname, x.Subdomain, SiteNaming.BuildPublicUrl(x.PublicScheme, x.Hostname, x.PublicPort), x.Domain,
            EncryptContainerId(x.ContainerResourceId), x.ContainerName, x.ContainerPort, x.Status, x.LastError, x.CreatedAt, x.UpdatedAt, x.UnpublishedAt)).ToArray();
    }

    [HttpPost]
    public async Task<IActionResult> Publish([FromBody] PublishWebsiteRequest request, CancellationToken cancellationToken)
    {
        long containerId;
        try { containerId = encryption.DecryptAsInt64(request.ContainerId, EncryptionPurpose.DockerSessionId); }
        catch { return BadRequest(new ResourceErrorDto(WebsiteHostingErrorCodes.ContainerNotFound, "Container not found.")); }
        try
        {
            PublishedSite site = await publisher.PublishAsync(
                new PublishSiteCommand(currentUser.Id, currentUser.IsAdmin, containerId, request.ContainerPort, request.HostedDomainId, request.Subdomain ?? "", null),
                cancellationToken);
            return Ok(site.Id);
        }
        catch (WebsiteHostingException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
    }

    [HttpPost("{id:long}/unpublish")]
    public async Task<IActionResult> Unpublish(long id, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.UnpublishAsync(id, currentUser.Id, isAdmin: false, force: false, cancellationToken);
            return NoContent();
        }
        catch (WebsiteHostingException ex) when (ex.Code == WebsiteHostingErrorCodes.SiteNotFound)
        {
            return NotFound();
        }
        catch (WebsiteHostingException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
    }

    [HttpGet("{id:long}/events")]
    public async Task<ActionResult<WebsiteEventDto[]>> Events(long id, CancellationToken cancellationToken)
    {
        if (!await db.PublishedSites.AnyAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken)) return NotFound();
        return await db.PublishedSiteEvents.AsNoTracking()
            .Where(x => x.PublishedSiteId == id)
            .OrderBy(x => x.Id)
            .Select(x => new WebsiteEventDto(x.Id, x.Action, x.IsSuccess, x.Message, x.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    private string EncryptContainerId(long id) => encryption.Encrypt(id, EncryptionPurpose.DockerSessionId);
}
