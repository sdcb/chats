using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.Containers.Dtos;
using Chats.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.Containers;

[ApiController]
[AuthorizeAdmin]
[Route("api/admin/container-catalog")]
public sealed class ContainerCatalogController(ChatsDB db) : ControllerBase
{
    private readonly ChatsDB _db = db;

    [HttpGet("runtime-nodes")]
    public async Task<ActionResult<IReadOnlyList<RuntimeNodeDto>>> RuntimeNodes(CancellationToken cancellationToken)
    {
        return await _db.ContainerRuntimeNodes
            .OrderBy(x => x.Name)
            .Select(x => new RuntimeNodeDto(x.Id, x.Name, x.AiName, x.Description, x.BackendType, x.Endpoint, x.Credential != null, x.IsEnabled, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("runtime-nodes")]
    public async Task<IActionResult> CreateRuntimeNode([FromBody] RuntimeNodeRequest request, CancellationToken cancellationToken)
    {
        ContainerRuntimeNode node = new()
        {
            Name = request.Name.Trim(),
            AiName = request.AIName.Trim(),
            Description = request.Description?.Trim(),
            BackendType = request.BackendType,
            Endpoint = string.IsNullOrWhiteSpace(request.Endpoint) ? null : request.Endpoint.Trim(),
            Credential = string.IsNullOrWhiteSpace(request.Credential) ? null : request.Credential.Trim(),
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ContainerRuntimeNodes.Add(node);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await GetRuntimeNodeDtoAsync(node.Id, cancellationToken));
    }

    [HttpPut("runtime-nodes/{id:int}")]
    public async Task<IActionResult> UpdateRuntimeNode(int id, [FromBody] RuntimeNodeRequest request, CancellationToken cancellationToken)
    {
        ContainerRuntimeNode? node = await _db.ContainerRuntimeNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.Name = request.Name.Trim();
        node.AiName = request.AIName.Trim();
        node.Description = request.Description?.Trim();
        node.BackendType = request.BackendType;
        node.Endpoint = request.Endpoint?.Trim();
        // Credentials are intentionally never returned by the API. An omitted or
        // blank credential on update therefore means "keep the existing value".
        // Send an explicit non-empty value to replace it, or null from a future
        // dedicated reset flow if clearing is required.
        if (request.Credential is not null && !string.IsNullOrWhiteSpace(request.Credential))
            node.Credential = request.Credential.Trim();
        node.IsEnabled = request.IsEnabled;
        node.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await GetRuntimeNodeDtoAsync(node.Id, cancellationToken));
    }

    [HttpPatch("runtime-nodes/{id:int}/enabled")]
    public async Task<IActionResult> SetRuntimeNodeEnabled(int id, [FromBody] EnabledRequest request, CancellationToken cancellationToken)
    {
        ContainerRuntimeNode? node = await _db.ContainerRuntimeNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.IsEnabled = request.IsEnabled;
        node.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, node.IsEnabled });
    }

    [HttpDelete("runtime-nodes/{id:int}")]
    public async Task<IActionResult> DeleteRuntimeNode(int id, CancellationToken cancellationToken)
    {
        ContainerRuntimeNode? node = await _db.ContainerRuntimeNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NoContent();
        bool inUse = await _db.ContainerResourceTemplates.AnyAsync(x => x.RuntimeNodeId == id, cancellationToken)
            || await _db.ContainerResources.AnyAsync(x => x.RuntimeNodeId == id, cancellationToken)
            || await _db.ContainerVolumes.AnyAsync(x => x.RuntimeNodeId == id, cancellationToken);
        if (inUse)
            return Conflict(new { Code = "RuntimeNodeInUse", Message = "The runtime node is still referenced by templates or resources." });
        _db.ContainerRuntimeNodes.Remove(node);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("images")]
    public async Task<ActionResult<IReadOnlyList<ContainerImage>>> Images(CancellationToken cancellationToken)
        => await _db.ContainerImages.OrderBy(x => x.Image).ToListAsync(cancellationToken);

    [HttpPut("images/{*image}")]
    public async Task<IActionResult> UpsertImage(string image, [FromBody] ImageRequest request, CancellationToken cancellationToken)
    {
        ContainerImage? entity = await _db.ContainerImages.SingleOrDefaultAsync(x => x.Image == image, cancellationToken);
        if (entity is null)
        {
            entity = new ContainerImage { Image = image };
            _db.ContainerImages.Add(entity);
        }
        entity.Description = request.Description;
        entity.IsEnabled = request.IsEnabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(entity);
    }

    [HttpDelete("images/{*image}")]
    public async Task<IActionResult> DeleteImage(string image, CancellationToken cancellationToken)
    {
        ContainerImage? entity = await _db.ContainerImages.SingleOrDefaultAsync(x => x.Image == image, cancellationToken);
        if (entity is not null)
        {
            _db.ContainerImages.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceTemplateDto>>> Templates(CancellationToken cancellationToken)
    {
        return await ProjectTemplates(_db.ContainerResourceTemplates.OrderBy(x => x.Name))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("templates/available")]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceTemplateDto>>> AvailableTemplates(CancellationToken cancellationToken)
    {
        return await ProjectTemplates(_db.ContainerResourceTemplates.Where(x => (x.Visibility & 1) != 0).OrderBy(x => x.Name))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateRequest request, CancellationToken cancellationToken)
    {
        ContainerResourceTemplate template = new() { CreatedAt = DateTime.UtcNow };
        IActionResult? error = await ApplyTemplateAsync(template, request, cancellationToken);
        if (error is not null) return error;
        _db.ContainerResourceTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ToTemplateResultAsync(template, cancellationToken));
    }

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] TemplateRequest request, CancellationToken cancellationToken)
    {
        ContainerResourceTemplate? template = await _db.ContainerResourceTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return NotFound();
        IActionResult? error = await ApplyTemplateAsync(template, request, cancellationToken);
        if (error is not null) return error;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ToTemplateResultAsync(template, cancellationToken));
    }

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken cancellationToken)
    {
        ContainerResourceTemplate? template = await _db.ContainerResourceTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return NoContent();
        _db.ContainerResourceTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("quotas")]
    public async Task<ActionResult<IReadOnlyList<ContainerQuotaDto>>> Quotas(CancellationToken cancellationToken)
        => await _db.UserContainerQuota
            .OrderBy(x => x.UserId)
            .Select(x => new ContainerQuotaDto(
                x.Id, x.UserId, x.User == null ? null : x.User.UserName,
                x.AllowCustomImage, x.AllowedNetworkModes, x.MaxContainerCount,
                x.MaxCpuCores, x.MaxMemoryBytes, x.MaxContainerProcesses,
                x.MaxVolumeBytes, x.MaxContainerCpuCores, x.MaxContainerMemoryBytes,
                x.MaxVolumeBytesPerVolume, x.UpdatedAt))
            .ToListAsync(cancellationToken);

    [HttpPut("quotas/{userId:int?}")]
    public async Task<IActionResult> UpsertQuota(int? userId, [FromBody] QuotaRequest request, CancellationToken cancellationToken)
    {
        UserContainerQuotum? quota = await _db.UserContainerQuota.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (quota is null)
        {
            quota = new UserContainerQuotum { UserId = userId };
            _db.UserContainerQuota.Add(quota);
        }
        quota.AllowCustomImage = request.AllowCustomImage;
        quota.AllowedNetworkModes = request.AllowedNetworkModes;
        quota.MaxContainerCount = request.MaxContainerCount;
        quota.MaxCpuCores = request.MaxCpuCores;
        quota.MaxMemoryBytes = request.MaxMemoryBytes;
        quota.MaxContainerProcesses = request.MaxContainerProcesses;
        quota.MaxVolumeBytes = request.MaxVolumeBytes;
        quota.MaxContainerCpuCores = request.MaxContainerCpuCores;
        quota.MaxContainerMemoryBytes = request.MaxContainerMemoryBytes;
        quota.MaxVolumeBytesPerVolume = request.MaxVolumeBytesPerVolume;
        quota.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ContainerQuotaDto(
            quota.Id, quota.UserId, null, quota.AllowCustomImage,
            quota.AllowedNetworkModes, quota.MaxContainerCount, quota.MaxCpuCores,
            quota.MaxMemoryBytes, quota.MaxContainerProcesses, quota.MaxVolumeBytes,
            quota.MaxContainerCpuCores, quota.MaxContainerMemoryBytes,
            quota.MaxVolumeBytesPerVolume, quota.UpdatedAt));
    }

    private async Task<IActionResult?> ApplyTemplateAsync(ContainerResourceTemplate template, TemplateRequest request, CancellationToken cancellationToken)
    {
        if (request.Visibility > 3 || request.CpuCores < 0 || request.MemoryBytes < 0 || request.MaxProcesses < 0 || request.DefaultVolumeBytes < 0)
            return BadRequest(new { Code = "InvalidConfiguration", Message = "Template values are invalid." });
        bool nodeExists = await _db.ContainerRuntimeNodes.AnyAsync(x => x.Id == request.RuntimeNodeId, cancellationToken);
        if (!nodeExists) return BadRequest(new { Code = "RuntimeNodeUnavailable", Message = "Runtime node was not found." });
        template.Name = request.Name.Trim();
        template.RuntimeNodeId = request.RuntimeNodeId;
        template.Image = request.Image.Trim();
        template.CpuCores = request.CpuCores;
        template.MemoryBytes = request.MemoryBytes;
        template.MaxProcesses = request.MaxProcesses;
        template.BackendNetworkName = string.IsNullOrWhiteSpace(request.BackendNetworkName) ? null : request.BackendNetworkName.Trim();
        template.DefaultVolumeBytes = request.DefaultVolumeBytes;
        template.Visibility = request.Visibility;
        template.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<object> ToTemplateResultAsync(ContainerResourceTemplate template, CancellationToken cancellationToken)
    {
        bool imageEnabled = await _db.ContainerImages.AnyAsync(x => x.Image == template.Image && x.IsEnabled, cancellationToken);
        ContainerResourceTemplateDto result = await ProjectTemplates(
            _db.ContainerResourceTemplates.Where(x => x.Id == template.Id))
            .SingleAsync(cancellationToken);
        return new
        {
            Template = result,
            WarningCode = imageEnabled ? null : "TemplateImageNotInEnabledCatalog",
        };
    }

    private static IQueryable<ContainerResourceTemplateDto> ProjectTemplates(IQueryable<ContainerResourceTemplate> query)
        => query.Select(x => new ContainerResourceTemplateDto(
            x.Id, x.Name, x.RuntimeNodeId, x.Image, x.CpuCores, x.MemoryBytes,
            x.MaxProcesses, x.BackendNetworkName, x.DefaultVolumeBytes, x.Visibility,
            x.CreatedAt, x.UpdatedAt,
            x.RuntimeNode == null ? null : new RuntimeNodeDto(
                x.RuntimeNode.Id, x.RuntimeNode.Name, x.RuntimeNode.AiName,
                x.RuntimeNode.Description, x.RuntimeNode.BackendType,
                x.RuntimeNode.Endpoint, x.RuntimeNode.Credential != null, x.RuntimeNode.IsEnabled,
                x.RuntimeNode.CreatedAt, x.RuntimeNode.UpdatedAt)));

    private Task<RuntimeNodeDto?> GetRuntimeNodeDtoAsync(int id, CancellationToken cancellationToken)
        => _db.ContainerRuntimeNodes
            .Where(x => x.Id == id)
            .Select(x => new RuntimeNodeDto(x.Id, x.Name, x.AiName, x.Description, x.BackendType,
                x.Endpoint, x.Credential != null, x.IsEnabled, x.CreatedAt, x.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
