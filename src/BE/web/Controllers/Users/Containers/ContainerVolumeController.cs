using Chats.BE.Infrastructure;
using Chats.BE.Services.Containers;
using Chats.DB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.BE.Controllers.Users.Containers.Dtos;
using Chats.BE.Services.UrlEncryption;

namespace Chats.BE.Controllers.Users.Containers;

[ApiController]
[Authorize]
[Route("api/volumes")]
public sealed class ContainerVolumeController(ChatsDB db, CurrentUser currentUser, ContainerResourceService resources, IUrlEncryptionService encryption) : ControllerBase
{
    private readonly ContainerResourceService _resources = resources;
    private readonly IUrlEncryptionService _encryption = encryption;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContainerVolume>>> List(CancellationToken cancellationToken)
        => await db.ContainerVolumes.Where(x => x.OwnerUserId == currentUser.Id && x.IsActive && x.IsStandalone).Include(x => x.ContainerVolumeMounts).ToListAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ContainerVolume>> Create([FromBody] CreateVolumeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _resources.CreateStandaloneVolumeAsync(request.Name, request.RuntimeNodeId, request.BackendVolumeId, request.DeclaredBytes, cancellationToken));
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new { ex.Code, ex.Message });
        }
    }

    [HttpPost("{id:long}/mounts")]
    public async Task<IActionResult> Mount(long id, [FromBody] MountVolumeRequest request, CancellationToken cancellationToken)
    {
        ContainerVolume? volume = await db.ContainerVolumes.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == currentUser.Id && x.IsStandalone && x.IsActive, cancellationToken);
        long containerId;
        try { containerId = _encryption.DecryptAsInt64(request.EncryptedContainerResourceId, EncryptionPurpose.DockerSessionId); }
        catch { return BadRequest("Invalid container id."); }
        bool containerOwned = await db.ContainerResources.AnyAsync(x => x.Id == containerId && x.OwnerUserId == currentUser.Id && x.DeletedAt == null, cancellationToken);
        if (volume is null || !containerOwned) return NotFound();
        db.ContainerVolumeMounts.Add(new ContainerVolumeMount
        {
            VolumeId = id,
            ContainerResourceId = containerId,
            ContainerPath = request.ContainerPath,
            IsReadOnly = request.IsReadOnly,
            IsActive = true,
            MountedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}/mounts/{mountId:long}")]
    public async Task<IActionResult> Unmount(long id, long mountId, CancellationToken cancellationToken)
    {
        ContainerVolumeMount? mount = await db.ContainerVolumeMounts
            .Include(x => x.Volume)
            .SingleOrDefaultAsync(x => x.Id == mountId && x.VolumeId == id && x.Volume.OwnerUserId == currentUser.Id && x.IsActive, cancellationToken);
        if (mount is null) return NotFound();
        mount.IsActive = false;
        mount.UnmountedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        ContainerVolume? volume = await db.ContainerVolumes.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == currentUser.Id && x.IsStandalone, cancellationToken);
        if (volume is null) return NotFound();
        volume.IsActive = false;
        volume.DeletedAt = DateTime.UtcNow;
        volume.UpdatedAt = volume.DeletedAt.Value;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
