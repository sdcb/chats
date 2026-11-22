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
    public async Task<ActionResult<IReadOnlyList<ContainerVolumeDto>>> List(CancellationToken cancellationToken)
    {
        var rows = await db.ContainerVolumes
            .AsNoTracking()
            .Where(x => x.OwnerUserId == currentUser.Id && x.IsActive && x.IsStandalone)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.RuntimeNodeId,
                RuntimeNodeName = x.RuntimeNode.AiName,
                x.BackendVolumeId,
                x.DeclaredBytes,
                x.CreatedAt,
                x.UpdatedAt,
                Mounts = x.ContainerVolumeMounts
                    .Where(m => m.IsActive)
                    .Select(m => new { m.Id, m.ContainerResourceId, ContainerName = m.ContainerResource.Name, m.ContainerPath, m.IsReadOnly, m.MountedAt })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new ContainerVolumeDto(
            x.Id, x.Name, x.RuntimeNodeId, x.RuntimeNodeName, x.BackendVolumeId, x.DeclaredBytes, x.CreatedAt, x.UpdatedAt,
            [.. x.Mounts.Select(m => new ContainerVolumeMountDto(
                m.Id, m.ContainerResourceId, _encryption.Encrypt(m.ContainerResourceId, EncryptionPurpose.DockerSessionId),
                m.ContainerName, m.ContainerPath, m.IsReadOnly, m.MountedAt))])).ToArray();
    }

    [HttpPost]
    public async Task<ActionResult<ContainerVolumeDto>> Create([FromBody] CreateVolumeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ContainerVolume volume = await _resources.CreateStandaloneVolumeAsync(request.Name, request.RuntimeNodeId, request.BackendVolumeId, request.DeclaredBytes, cancellationToken);
            return Ok(new ContainerVolumeDto(volume.Id, volume.Name, volume.RuntimeNodeId, null, volume.BackendVolumeId, volume.DeclaredBytes, volume.CreatedAt, volume.UpdatedAt, []));
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
    }

    [HttpPost("{id:long}/mounts")]
    public async Task<IActionResult> Mount(long id, [FromBody] MountVolumeRequest request, CancellationToken cancellationToken)
    {
        long containerId;
        try { containerId = _encryption.DecryptAsInt64(request.EncryptedContainerResourceId, EncryptionPurpose.DockerSessionId); }
        catch { return BadRequest(new ResourceErrorDto(ContainerResourceErrorCodes.InvalidConfiguration, "Invalid container id.")); }
        return await Execute(() => _resources.MountVolumeAsync(id, containerId, request.ContainerPath, request.IsReadOnly, cancellationToken));
    }

    [HttpDelete("{id:long}/mounts/{mountId:long}")]
    public Task<IActionResult> Unmount(long id, long mountId, CancellationToken cancellationToken)
        => Execute(() => _resources.UnmountVolumeAsync(id, mountId, cancellationToken));

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        => Execute(() => _resources.DeleteStandaloneVolumeAsync(id, cancellationToken));

    private async Task<IActionResult> Execute(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
