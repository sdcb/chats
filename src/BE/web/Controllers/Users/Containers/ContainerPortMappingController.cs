using Chats.BE.Controllers.Users.Containers.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Containers;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Users.Containers;

[ApiController]
[Authorize]
[Route("api/containers/{encryptedId}/port-mappings")]
public sealed class ContainerPortMappingController(
    ChatsDB db,
    CurrentUser currentUser,
    ContainerPortMappingService portMappings,
    IUrlEncryptionService encryption) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContainerPortMappingDto>>> List(string encryptedId, CancellationToken cancellationToken)
    {
        long? id = await GetOwnedResourceIdAsync(encryptedId, cancellationToken);
        if (id is null) return NotFound();
        IReadOnlyList<ContainerPortMapping> mappings = await portMappings.ListAsync(id.Value, cancellationToken);
        return mappings.Select(ToDto).ToArray();
    }

    [HttpPost]
    public async Task<ActionResult<ContainerPortMappingDto>> Create(string encryptedId, [FromBody] CreateContainerPortMappingRequest request, CancellationToken cancellationToken)
    {
        long? id = await GetOwnedResourceIdAsync(encryptedId, cancellationToken);
        if (id is null) return NotFound();
        try
        {
            ContainerPortMapping mapping = await portMappings.EnsureMappingAsync(id.Value, request.ContainerPort, request.Protocol ?? ContainerPortMappingService.ProtocolTcp, cancellationToken);
            return Ok(ToDto(mapping));
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
    }

    [HttpDelete("{mappingId:long}")]
    public async Task<IActionResult> Delete(string encryptedId, long mappingId, CancellationToken cancellationToken)
    {
        long? id = await GetOwnedResourceIdAsync(encryptedId, cancellationToken);
        if (id is null) return NotFound();
        if (!await db.ContainerPortMappings.AnyAsync(x => x.Id == mappingId && x.ContainerResourceId == id.Value, cancellationToken)) return NotFound();
        try
        {
            await portMappings.RemoveMappingAsync(mappingId, cancellationToken);
            return NoContent();
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
    }

    private async Task<long?> GetOwnedResourceIdAsync(string encryptedId, CancellationToken cancellationToken)
    {
        long id;
        try { id = encryption.DecryptAsInt64(encryptedId, EncryptionPurpose.DockerSessionId); }
        catch { return null; }
        bool owned = await db.ContainerResources.AnyAsync(x => x.Id == id && x.OwnerUserId == currentUser.Id, cancellationToken);
        return owned ? id : null;
    }

    private static ContainerPortMappingDto ToDto(ContainerPortMapping x) => new(
        x.Id,
        x.ContainerPort,
        x.HostPort,
        x.Protocol,
        x.RuntimeNodeId,
        ContainerPortMappingService.GetPublicHost(x.RuntimeNode),
        ContainerPortMappingService.GetUpstreamAddress(x),
        x.CreatedAt);
}
