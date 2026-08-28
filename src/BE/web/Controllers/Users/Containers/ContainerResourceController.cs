using Chats.BE.Services.Containers;
using Chats.DB;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Controllers.Users.Containers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Containers;

[ApiController]
[Authorize]
[Route("api/containers")]
public sealed class ContainerResourceController(
    ContainerResourceService resources,
    IUrlEncryptionService encryption) : ControllerBase
{
    private readonly ContainerResourceService _resources = resources;
    private readonly IUrlEncryptionService _encryption = encryption;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceDto>>> List([FromQuery] bool includeDeleted, CancellationToken cancellationToken)
    {
        IReadOnlyList<ContainerResource> resources = await _resources.ListMineAsync(includeDeleted, cancellationToken);
        return resources.Select(ToDto).ToArray();
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ContainerTemplateDto>>> Templates(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContainerResourceTemplate> templates = await _resources.ListTemplatesAsync(false, cancellationToken);
        return templates.Select(x => new ContainerTemplateDto(x.Id, x.Name, x.RuntimeNodeId, x.RuntimeNode.AIName, x.Image, x.CpuCores, x.MemoryBytes, x.MaxProcesses, x.BackendNetworkName, x.DefaultVolumeBytes, x.Visibility)).ToArray();
    }

    [HttpGet("for-chat/{encryptedChatId}")]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceDto>>> ForChat(string encryptedChatId, CancellationToken cancellationToken)
    {
        int chatId = _encryption.DecryptChatId(encryptedChatId);
        IReadOnlyList<ContainerResource> resources = await _resources.ListMineAsync(false, cancellationToken);
        ContainerResource[] visible = resources
            .Where(x => x.OwnerChatId == chatId || x.ChatContainerResourceAccesses.Any(a => a.ChatId == chatId))
            .ToArray();
        return visible.Select(ToDto).ToArray();
    }

    [HttpPost]
    public async Task<ActionResult<ContainerResourceDto>> Create([FromBody] CreateContainerResourceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ContainerResource resource = await _resources.CreateAsync(
                request.Name ?? string.Empty,
                request.IsPermanent,
                request.TemplateId,
                request.Image,
                request.CpuCores,
                request.MemoryBytes,
                request.MaxProcesses,
                request.BackendNetworkName,
                string.IsNullOrWhiteSpace(request.OwnerChatId) ? null : _encryption.DecryptChatId(request.OwnerChatId),
                null,
                cancellationToken);
            return CreatedAtAction(nameof(Get), new { encryptedId = _encryption.Encrypt(resource.Id, EncryptionPurpose.DockerSessionId) }, ToDto(resource));
        }
        catch (ContainerResourceException ex)
        {
            return BadRequest(new ResourceErrorDto(ex.Code, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{encryptedId}")]
    public async Task<ActionResult<ContainerResourceDto>> Get(string encryptedId, CancellationToken cancellationToken)
    {
        long id = DecryptId(encryptedId);
        ContainerResource? resource = (await _resources.ListMineAsync(true, cancellationToken)).FirstOrDefault(x => x.Id == id);
        return resource is null ? NotFound() : ToDto(resource);
    }

    [HttpPost("{encryptedId}/start")]
    public Task<IActionResult> Start(string encryptedId, CancellationToken cancellationToken) => Execute(DecryptId(encryptedId), _resources.StartAsync, cancellationToken);

    [HttpPost("{encryptedId}/stop")]
    public Task<IActionResult> Stop(string encryptedId, CancellationToken cancellationToken) => Execute(DecryptId(encryptedId), _resources.StopAsync, cancellationToken);

    [HttpDelete("{encryptedId}")]
    public Task<IActionResult> Delete(string encryptedId, CancellationToken cancellationToken) => Execute(DecryptId(encryptedId), _resources.DeleteAsync, cancellationToken);

    [HttpPatch("{encryptedId}")]
    public async Task<IActionResult> Update(string encryptedId, [FromBody] UpdateContainerResourceRequest request, CancellationToken cancellationToken)
    {
        long id = DecryptId(encryptedId);
        try
        {
            await _resources.UpdateAsync(id, request.CpuCores, request.MemoryBytes, request.MaxProcesses, request.BackendNetworkName, cancellationToken);
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

    [HttpPost("{encryptedId}/chats/{encryptedChatId}/grant")]
    public async Task<IActionResult> Grant(string encryptedId, string encryptedChatId, CancellationToken cancellationToken)
    {
        long id = DecryptId(encryptedId);
        int chatId = _encryption.DecryptChatId(encryptedChatId);
        try
        {
            await _resources.GrantChatAccessAsync(chatId, id, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{encryptedId}/chats/{encryptedChatId}/grant")]
    public async Task<IActionResult> Revoke(string encryptedId, string encryptedChatId, CancellationToken cancellationToken)
    {
        long id = DecryptId(encryptedId);
        int chatId = _encryption.DecryptChatId(encryptedChatId);
        await _resources.RevokeChatAccessAsync(chatId, id, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> Execute(long id, Func<long, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action(id, cancellationToken);
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

    private ContainerResourceDto ToDto(ContainerResource resource) => new(
        _encryption.Encrypt(resource.Id, EncryptionPurpose.DockerSessionId),
        resource.Name,
        resource.IsPermanent,
        resource.Image,
        resource.CpuCores,
        resource.MemoryBytes,
        resource.MaxProcesses,
        resource.BackendNetworkName,
        resource.RuntimeNode?.AIName,
        resource.Ip,
        resource.DeletedAt is not null,
        resource.StoppedAt is not null,
        resource.CreatedAt,
        resource.UpdatedAt,
        resource.CleanupAt,
        [.. resource.ChatContainerResourceAccesses.Select(x => _encryption.Encrypt(x.ChatId, EncryptionPurpose.ChatId))],
        resource.OwnerTurnId);

    private long DecryptId(string encryptedId) => _encryption.DecryptAsInt64(encryptedId, EncryptionPurpose.DockerSessionId);
}
