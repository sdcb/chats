using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.Containers.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;

namespace Chats.BE.Controllers.Admin.Containers;

[ApiController]
[AuthorizeAdmin]
[Route("api/admin/container-catalog")]
public sealed class ContainerCatalogController(ChatsDB db) : ControllerBase
{
    private readonly ChatsDB _db = db;
    private const int ResourceExportLimit = 10000;

    [HttpGet("resources")]
    public async Task<ActionResult<PagedResult<ContainerResourceAdminDto>>> Resources(
        [FromQuery] ContainerResourceQuery query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        IQueryable<ContainerResourceAdminDto> rows = BuildResourceQuery(query)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(ProjectResource());

        return Ok(await PagedResult.FromQuery(rows, query, cancellationToken));
    }

    [HttpGet("resources/export")]
    public async Task<IActionResult> ExportResources(
        [FromQuery] ContainerResourceExportQuery query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        ContainerResourceAdminDto[] rows = await BuildResourceQuery(query)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(ProjectResource())
            .Take(ResourceExportLimit)
            .ToArrayAsync(cancellationToken);

        List<string>? selectedColumns = ParseColumns(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .Select(row => BuildResourceExportRow(row, selectedColumns))
            .ToList();

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"container-resources-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet("runtime-nodes")]
    public async Task<ActionResult<IReadOnlyList<RuntimeNodeDto>>> RuntimeNodes(
        [FromQuery] RuntimeNodeQuery query,
        CancellationToken cancellationToken)
    {
        return await BuildRuntimeNodeQuery(query)
            .OrderBy(x => x.Name)
            .Select(x => new RuntimeNodeDto(x.Id, x.Name, x.AiName, x.Description, x.BackendType, x.Endpoint, x.Credential != null, x.IsEnabled, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("runtime-nodes/export")]
    public async Task<IActionResult> ExportRuntimeNodes(
        [FromQuery] RuntimeNodeExportQuery query,
        CancellationToken cancellationToken)
    {
        RuntimeNodeDto[] rows = await BuildRuntimeNodeQuery(query)
            .OrderBy(x => x.Name)
            .Select(x => new RuntimeNodeDto(x.Id, x.Name, x.AiName, x.Description, x.BackendType, x.Endpoint, x.Credential != null, x.IsEnabled, x.CreatedAt, x.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        List<string>? selectedColumns = ParseColumns(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .Select(row => BuildRuntimeNodeExportRow(row, selectedColumns))
            .ToList();
        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"runtime-nodes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
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
    public async Task<ActionResult<IReadOnlyList<ContainerImageDto>>> Images(
        [FromQuery] ImageQuery query,
        CancellationToken cancellationToken)
        => await BuildImageQuery(query)
            .OrderBy(x => x.Image)
            .Select(x => new ContainerImageDto(x.Id, x.Image, x.Description, x.IsEnabled))
            .ToListAsync(cancellationToken);

    [HttpGet("images/export")]
    public async Task<IActionResult> ExportImages(
        [FromQuery] ImageExportQuery query,
        CancellationToken cancellationToken)
    {
        ContainerImageDto[] rows = await BuildImageQuery(query)
            .OrderBy(x => x.Image)
            .Select(x => new ContainerImageDto(x.Id, x.Image, x.Description, x.IsEnabled))
            .ToArrayAsync(cancellationToken);
        List<string>? selectedColumns = ParseColumns(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .Select(row => BuildImageExportRow(row, selectedColumns))
            .ToList();
        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"container-images-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpPost("images")]
    public async Task<IActionResult> CreateImage([FromBody] ImageRequest request, CancellationToken cancellationToken)
    {
        string? image = NormalizeImageName(request.Image);
        if (image is null)
            return BadRequest(new { Code = "InvalidImageName", Message = "Image name is required." });
        if (await _db.ContainerImages.AnyAsync(x => x.Image == image, cancellationToken))
            return Conflict(new { Code = "ImageAlreadyExists", Message = "An image with this name already exists." });

        ContainerImage entity = new()
        {
            Image = image,
            Description = NormalizeDescription(request.Description),
            IsEnabled = request.IsEnabled,
        };
        _db.ContainerImages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ContainerImageDto(entity.Id, entity.Image, entity.Description, entity.IsEnabled));
    }

    [HttpPut("images/{id:int}")]
    public async Task<IActionResult> UpdateImage(int id, [FromBody] ImageRequest request, CancellationToken cancellationToken)
    {
        ContainerImage? entity = await _db.ContainerImages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        string? image = NormalizeImageName(request.Image);
        if (image is null)
            return BadRequest(new { Code = "InvalidImageName", Message = "Image name is required." });
        if (await _db.ContainerImages.AnyAsync(x => x.Id != id && x.Image == image, cancellationToken))
            return Conflict(new { Code = "ImageAlreadyExists", Message = "An image with this name already exists." });

        string previousImage = entity.Image;
        entity.Image = image;
        entity.Description = NormalizeDescription(request.Description);
        entity.IsEnabled = request.IsEnabled;

        // Templates keep the selected image name as their runtime input. Keep
        // those references in sync when an administrator renames a catalog
        // entry, while existing container resources retain their historical
        // image snapshot.
        if (!String.Equals(previousImage, image, StringComparison.Ordinal))
        {
            DateTime now = DateTime.UtcNow;
            List<ContainerResourceTemplate> templates = await _db.ContainerResourceTemplates
                .Where(x => x.Image == previousImage)
                .ToListAsync(cancellationToken);
            foreach (ContainerResourceTemplate template in templates)
            {
                template.Image = image;
                template.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ContainerImageDto(entity.Id, entity.Image, entity.Description, entity.IsEnabled));
    }

    [HttpDelete("images/{id:int}")]
    public async Task<IActionResult> DeleteImage(int id, CancellationToken cancellationToken)
    {
        ContainerImage? entity = await _db.ContainerImages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NoContent();
        if (await _db.ContainerResourceTemplates.AnyAsync(x => x.Image == entity.Image, cancellationToken))
            return Conflict(new
            {
                Code = "ImageInUse",
                Message = "The image is still referenced by one or more resource templates.",
            });

        _db.ContainerImages.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceTemplateDto>>> Templates(
        [FromQuery] TemplateQuery query,
        CancellationToken cancellationToken)
    {
        return await ProjectTemplates(BuildTemplateQuery(query).OrderBy(x => x.Name))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("templates/export")]
    public async Task<IActionResult> ExportTemplates(
        [FromQuery] TemplateExportQuery query,
        CancellationToken cancellationToken)
    {
        ContainerResourceTemplateDto[] rows = await ProjectTemplates(BuildTemplateQuery(query).OrderBy(x => x.Name))
            .ToArrayAsync(cancellationToken);
        List<string>? selectedColumns = ParseColumns(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .Select(row => BuildTemplateExportRow(row, selectedColumns))
            .ToList();
        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"container-resource-templates-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
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
    public async Task<ActionResult<IReadOnlyList<ContainerQuotaDto>>> Quotas(
        [FromQuery] QuotaQuery query,
        CancellationToken cancellationToken)
        => await BuildQuotaQuery(query)
            .OrderBy(x => x.UserId)
            .Select(x => new ContainerQuotaDto(
                x.Id, x.UserId, x.User == null ? null : x.User.UserName,
                x.AllowCustomImage, x.AllowedNetworkModes, x.MaxContainerCount,
                x.MaxCpuCores, x.MaxMemoryBytes, x.MaxContainerProcesses,
                x.MaxVolumeBytes, x.MaxContainerCpuCores, x.MaxContainerMemoryBytes,
                x.MaxVolumeBytesPerVolume, x.UpdatedAt))
            .ToListAsync(cancellationToken);

    [HttpGet("quotas/export")]
    public async Task<IActionResult> ExportQuotas(
        [FromQuery] QuotaExportQuery query,
        CancellationToken cancellationToken)
    {
        ContainerQuotaDto[] rows = await BuildQuotaQuery(query)
            .OrderBy(x => x.UserId)
            .Select(x => new ContainerQuotaDto(
                x.Id, x.UserId, x.User == null ? null : x.User.UserName,
                x.AllowCustomImage, x.AllowedNetworkModes, x.MaxContainerCount,
                x.MaxCpuCores, x.MaxMemoryBytes, x.MaxContainerProcesses,
                x.MaxVolumeBytes, x.MaxContainerCpuCores, x.MaxContainerMemoryBytes,
                x.MaxVolumeBytesPerVolume, x.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        List<string>? selectedColumns = ParseColumns(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .Select(row => BuildQuotaExportRow(row, selectedColumns))
            .ToList();
        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"container-quotas-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpPut("quotas/{userId:int?}")]
    public async Task<IActionResult> UpsertQuota(int? userId, [FromBody] QuotaRequest request, CancellationToken cancellationToken)
    {
        if (userId.HasValue && !await _db.Users.AnyAsync(x => x.Id == userId.Value, cancellationToken))
            return BadRequest(new { Code = "UserNotFound", Message = "The selected user was not found." });

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

    [HttpDelete("quotas/{userId:int}")]
    public async Task<IActionResult> DeleteUserQuota(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
            return BadRequest(new { Code = "InvalidUserId", Message = "A user quota requires a positive user ID." });

        UserContainerQuotum? quota = await _db.UserContainerQuota
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (quota is null)
            return NoContent();

        // UserId = null is the default inherited quota and cannot be reached
        // through this route, but keep this guard in case the route changes.
        if (quota.UserId is null)
            return Conflict(new { Code = "DefaultQuotaProtected", Message = "The default inherited quota cannot be deleted." });

        _db.UserContainerQuota.Remove(quota);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    private IQueryable<ContainerRuntimeNode> BuildRuntimeNodeQuery(RuntimeNodeQuery query)
        => BuildRuntimeNodeQuery(query.Query, query.BackendType, query.Enabled);

    private IQueryable<ContainerRuntimeNode> BuildRuntimeNodeQuery(RuntimeNodeExportQuery query)
        => BuildRuntimeNodeQuery(query.Query, query.BackendType, query.Enabled);

    private IQueryable<ContainerRuntimeNode> BuildRuntimeNodeQuery(string? search, byte? backendType, bool? enabled)
    {
        IQueryable<ContainerRuntimeNode> rows = _db.ContainerRuntimeNodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            bool hasId = int.TryParse(keyword, out int id);
            rows = rows.Where(x =>
                (hasId && x.Id == id) ||
                EF.Functions.Like(x.Name, $"%{keyword}%") ||
                EF.Functions.Like(x.AiName, $"%{keyword}%") ||
                (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")) ||
                (x.Endpoint != null && EF.Functions.Like(x.Endpoint, $"%{keyword}%")));
        }
        if (backendType.HasValue)
            rows = rows.Where(x => x.BackendType == backendType.Value);
        if (enabled.HasValue)
            rows = rows.Where(x => x.IsEnabled == enabled.Value);
        return rows;
    }

    private IQueryable<ContainerResourceTemplate> BuildTemplateQuery(TemplateQuery query)
        => BuildTemplateQuery(query.Query, query.RuntimeNodeId, query.Visibility);

    private IQueryable<ContainerResourceTemplate> BuildTemplateQuery(TemplateExportQuery query)
        => BuildTemplateQuery(query.Query, query.RuntimeNodeId, query.Visibility);

    private IQueryable<ContainerResourceTemplate> BuildTemplateQuery(string? search, int? runtimeNodeId, byte? visibility)
    {
        IQueryable<ContainerResourceTemplate> rows = _db.ContainerResourceTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            bool hasId = int.TryParse(keyword, out int id);
            rows = rows.Where(x =>
                (hasId && x.Id == id) ||
                EF.Functions.Like(x.Name, $"%{keyword}%") ||
                EF.Functions.Like(x.Image, $"%{keyword}%") ||
                (x.BackendNetworkName != null && EF.Functions.Like(x.BackendNetworkName, $"%{keyword}%")) ||
                (x.RuntimeNode != null &&
                    (EF.Functions.Like(x.RuntimeNode.Name, $"%{keyword}%") ||
                     EF.Functions.Like(x.RuntimeNode.AiName, $"%{keyword}%"))));
        }
        if (runtimeNodeId.HasValue)
            rows = rows.Where(x => x.RuntimeNodeId == runtimeNodeId.Value);
        if (visibility.HasValue)
            rows = rows.Where(x => x.Visibility == visibility.Value);
        return rows;
    }

    private IQueryable<ContainerImage> BuildImageQuery(ImageQuery query)
        => BuildImageQuery(query.Query, query.Enabled);

    private IQueryable<ContainerImage> BuildImageQuery(ImageExportQuery query)
        => BuildImageQuery(query.Query, query.Enabled);

    private IQueryable<ContainerImage> BuildImageQuery(string? search, bool? enabled)
    {
        IQueryable<ContainerImage> rows = _db.ContainerImages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            bool hasId = int.TryParse(keyword, out int id);
            rows = rows.Where(x =>
                (hasId && x.Id == id) ||
                EF.Functions.Like(x.Image, $"%{keyword}%") ||
                (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")));
        }
        if (enabled.HasValue)
            rows = rows.Where(x => x.IsEnabled == enabled.Value);
        return rows;
    }

    private IQueryable<UserContainerQuotum> BuildQuotaQuery(QuotaQuery query)
        => BuildQuotaQuery(query.Query, query.AllowCustomImage, query.Scope);

    private IQueryable<UserContainerQuotum> BuildQuotaQuery(QuotaExportQuery query)
        => BuildQuotaQuery(query.Query, query.AllowCustomImage, query.Scope);

    private IQueryable<UserContainerQuotum> BuildQuotaQuery(string? search, bool? allowCustomImage, string? scope)
    {
        IQueryable<UserContainerQuotum> rows = _db.UserContainerQuota.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            bool hasId = int.TryParse(keyword, out int id);
            rows = rows.Where(x =>
                (hasId && (x.Id == id || x.UserId == id)) ||
                (x.User != null &&
                    (EF.Functions.Like(x.User.UserName, $"%{keyword}%") ||
                     (x.User.DisplayName != null && EF.Functions.Like(x.User.DisplayName, $"%{keyword}%")))) ||
                (x.UserId == null && EF.Functions.Like("default", $"%{keyword}%")));
        }
        if (allowCustomImage.HasValue)
            rows = rows.Where(x => x.AllowCustomImage == allowCustomImage.Value);
        switch (scope?.Trim().ToLowerInvariant())
        {
            case "default":
                rows = rows.Where(x => x.UserId == null);
                break;
            case "user":
                rows = rows.Where(x => x.UserId != null);
                break;
        }
        return rows;
    }

    private static Dictionary<string, object?> BuildRuntimeNodeExportRow(
        RuntimeNodeDto row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        IEnumerable<string> columns = selectedColumns ?? ["id", "name", "aiName", "backendType", "isEnabled", "endpoint", "hasCredential", "description", "createdAt", "updatedAt"];
        Dictionary<string, object?> result = new();
        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": result["ID"] = row.Id; break;
                case "name": result["Name"] = row.Name; break;
                case "aiName": result["AI name"] = row.AIName; break;
                case "backendType": result["Backend"] = row.BackendType switch { 1 => "Docker", 2 => "Windows Docker", 3 => "Kubernetes", _ => "Other" }; break;
                case "isEnabled": result["Enabled"] = row.IsEnabled; break;
                case "endpoint": result["Endpoint"] = row.Endpoint; break;
                case "hasCredential": result["Credential configured"] = row.HasCredential; break;
                case "description": result["Description"] = row.Description; break;
                case "createdAt": result["Created"] = row.CreatedAt; break;
                case "updatedAt": result["Updated"] = row.UpdatedAt; break;
            }
        }
        return result;
    }

    private static Dictionary<string, object?> BuildImageExportRow(
        ContainerImageDto row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        IEnumerable<string> columns = selectedColumns ?? ["id", "image", "description", "isEnabled"];
        Dictionary<string, object?> result = new();
        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": result["ID"] = row.Id; break;
                case "image": result["Image"] = row.Image; break;
                case "description": result["Description"] = row.Description; break;
                case "isEnabled": result["Enabled"] = row.IsEnabled; break;
            }
        }
        return result;
    }

    private static Dictionary<string, object?> BuildTemplateExportRow(
        ContainerResourceTemplateDto row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        IEnumerable<string> columns = selectedColumns ?? ["id", "name", "runtimeNode", "image", "visibility", "cpuCores", "memoryBytes", "maxProcesses", "backendNetworkName", "defaultVolumeBytes", "createdAt", "updatedAt"];
        Dictionary<string, object?> result = new();
        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": result["ID"] = row.Id; break;
                case "name": result["Name"] = row.Name; break;
                case "runtimeNode": result["Runtime node"] = row.RuntimeNode?.Name ?? $"#{row.RuntimeNodeId}"; break;
                case "image": result["Image"] = row.Image; break;
                case "visibility": result["Visibility"] = row.Visibility; break;
                case "cpuCores": result["CPU cores"] = row.CpuCores; break;
                case "memoryBytes": result["Memory bytes"] = row.MemoryBytes; break;
                case "maxProcesses": result["Max processes"] = row.MaxProcesses; break;
                case "backendNetworkName": result["Network"] = row.BackendNetworkName; break;
                case "defaultVolumeBytes": result["Default volume bytes"] = row.DefaultVolumeBytes; break;
                case "createdAt": result["Created"] = row.CreatedAt; break;
                case "updatedAt": result["Updated"] = row.UpdatedAt; break;
            }
        }
        return result;
    }

    private static Dictionary<string, object?> BuildQuotaExportRow(
        ContainerQuotaDto row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        IEnumerable<string> columns = selectedColumns ?? ["id", "user", "allowedNetworkModes", "allowCustomImage", "maxContainerCount", "maxContainerProcesses", "maxCpuCores", "maxMemoryBytes", "maxVolumeBytes", "maxContainerCpuCores", "maxContainerMemoryBytes", "maxVolumeBytesPerVolume", "updatedAt"];
        Dictionary<string, object?> result = new();
        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": result["ID"] = row.Id; break;
                case "user": result["User"] = row.UserName ?? (row.UserId.HasValue ? $"User #{row.UserId}" : "Default inherited quota"); break;
                case "allowedNetworkModes": result["Allowed networks"] = row.AllowedNetworkModes; break;
                case "allowCustomImage": result["Custom images"] = row.AllowCustomImage; break;
                case "maxContainerCount": result["Container limit"] = row.MaxContainerCount; break;
                case "maxContainerProcesses": result["Process limit"] = row.MaxContainerProcesses; break;
                case "maxCpuCores": result["CPU limit"] = row.MaxCpuCores; break;
                case "maxMemoryBytes": result["Memory limit"] = row.MaxMemoryBytes; break;
                case "maxVolumeBytes": result["Volume limit"] = row.MaxVolumeBytes; break;
                case "maxContainerCpuCores": result["Max CPU per container"] = row.MaxContainerCpuCores; break;
                case "maxContainerMemoryBytes": result["Max memory per container"] = row.MaxContainerMemoryBytes; break;
                case "maxVolumeBytesPerVolume": result["Max volume bytes per volume"] = row.MaxVolumeBytesPerVolume; break;
                case "updatedAt": result["Updated"] = row.UpdatedAt; break;
            }
        }
        return result;
    }

    private IQueryable<ContainerResource> BuildResourceQuery(ContainerResourceQuery query)
        => BuildResourceQuery(query.Id, query.Query, query.Owner, query.RuntimeNodeId, query.Status, query.Permanent);

    private IQueryable<ContainerResource> BuildResourceQuery(ContainerResourceExportQuery query)
        => BuildResourceQuery(query.Id, query.Query, query.Owner, query.RuntimeNodeId, query.Status, query.Permanent);

    private IQueryable<ContainerResource> BuildResourceQuery(
        string? id,
        string? search,
        string? owner,
        int? runtimeNodeId,
        string? status,
        bool? permanent)
    {
        IQueryable<ContainerResource> rows = _db.ContainerResources.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!long.TryParse(id.Trim(), out long parsedId))
                return rows.Where(_ => false);
            rows = rows.Where(x => x.Id == parsedId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            rows = rows.Where(x =>
                EF.Functions.Like(x.Name, $"%{keyword}%") ||
                EF.Functions.Like(x.Image, $"%{keyword}%") ||
                EF.Functions.Like(x.BackendResourceId, $"%{keyword}%") ||
                (x.Ip != null && EF.Functions.Like(x.Ip, $"%{keyword}%")) ||
                (x.RuntimeNode != null &&
                    (EF.Functions.Like(x.RuntimeNode.Name, $"%{keyword}%") ||
                     EF.Functions.Like(x.RuntimeNode.AiName, $"%{keyword}%"))) ||
                (x.OwnerChat != null && EF.Functions.Like(x.OwnerChat.Title, $"%{keyword}%")));
        }

        if (!string.IsNullOrWhiteSpace(owner))
        {
            string keyword = owner.Trim();
            if (int.TryParse(keyword, out int ownerId))
            {
                rows = rows.Where(x => x.OwnerUserId == ownerId ||
                    (x.OwnerUser != null && EF.Functions.Like(x.OwnerUser.UserName, $"%{keyword}%")) ||
                    (x.OwnerUser != null && EF.Functions.Like(x.OwnerUser.DisplayName, $"%{keyword}%")));
            }
            else
            {
                rows = rows.Where(x =>
                    (x.OwnerUser != null && EF.Functions.Like(x.OwnerUser.UserName, $"%{keyword}%")) ||
                    (x.OwnerUser != null && EF.Functions.Like(x.OwnerUser.DisplayName, $"%{keyword}%")));
            }
        }

        if (runtimeNodeId.HasValue)
            rows = rows.Where(x => x.RuntimeNodeId == runtimeNodeId.Value);

        if (permanent.HasValue)
            rows = rows.Where(x => x.IsPermanent == permanent.Value);

        switch (status?.Trim().ToLowerInvariant())
        {
            case "active":
            case "running":
                rows = rows.Where(x => x.DeletedAt == null && x.StoppedAt == null);
                break;
            case "stopped":
                rows = rows.Where(x => x.DeletedAt == null && x.StoppedAt != null);
                break;
            case "deleted":
                rows = rows.Where(x => x.DeletedAt != null);
                break;
        }

        return rows;
    }

    private static System.Linq.Expressions.Expression<Func<ContainerResource, ContainerResourceAdminDto>> ProjectResource()
        => x => new ContainerResourceAdminDto(
            x.Id,
            x.OwnerUserId,
            x.OwnerUser == null ? null : x.OwnerUser.UserName,
            x.OwnerUser == null ? null : x.OwnerUser.DisplayName,
            x.OwnerChatId,
            x.OwnerChat == null ? null : x.OwnerChat.Title,
            x.OwnerTurnId,
            x.RuntimeNodeId,
            x.RuntimeNode == null ? null : x.RuntimeNode.Name,
            x.RuntimeNode == null ? null : x.RuntimeNode.AiName,
            x.IsPermanent,
            x.BackendResourceId,
            x.Ip,
            x.Name,
            x.Image,
            x.ShellPrefix,
            x.CpuCores,
            x.MemoryBytes,
            x.MaxProcesses,
            x.BackendNetworkName,
            x.CreatedAt,
            x.UpdatedAt,
            x.LastActiveAt,
            x.StoppedAt,
            x.DeletedAt,
            x.CleanupAt,
            x.ContainerVolume == null ? null : x.ContainerVolume.DeclaredBytes,
            x.ContainerVolumeMounts.Count(),
            x.ChatContainerResourceAccesses.Count());

    private static List<string>? ParseColumns(string? columns)
    {
        if (string.IsNullOrWhiteSpace(columns))
            return null;

        List<string> result = columns
            .Split('~', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, object?> BuildResourceExportRow(
        ContainerResourceAdminDto row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        Dictionary<string, object?> exportRow = new();
        IEnumerable<string> columns = selectedColumns ??
        [
            "id", "ownerUserId", "ownerUserName", "ownerChatId", "ownerChatTitle",
            "runtimeNodeName", "runtimeNodeAIName", "name", "image", "status",
            "isPermanent", "backendResourceId", "ip", "cpuCores", "memoryBytes",
            "maxProcesses", "backendNetworkName", "createdAt", "updatedAt", "lastActiveAt",
            "stoppedAt", "deletedAt", "cleanupAt", "volumeDeclaredBytes", "volumeMountCount",
            "chatAccessCount"
        ];

        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": exportRow["ID"] = row.Id; break;
                case "ownerUserId": exportRow["Owner User ID"] = row.OwnerUserId; break;
                case "ownerUserName": exportRow["Owner Username"] = row.OwnerUserName; break;
                case "ownerDisplayName": exportRow["Owner Display Name"] = row.OwnerDisplayName; break;
                case "ownerChatId": exportRow["Owner Chat ID"] = row.OwnerChatId; break;
                case "ownerChatTitle": exportRow["Owner Chat Title"] = row.OwnerChatTitle; break;
                case "ownerTurnId": exportRow["Owner Turn ID"] = row.OwnerTurnId; break;
                case "runtimeNodeId": exportRow["Runtime Node ID"] = row.RuntimeNodeId; break;
                case "runtimeNodeName": exportRow["Runtime Node"] = row.RuntimeNodeName; break;
                case "runtimeNodeAIName": exportRow["Runtime AI"] = row.RuntimeNodeAIName; break;
                case "name": exportRow["Name"] = row.Name; break;
                case "image": exportRow["Image"] = row.Image; break;
                case "status": exportRow["Status"] = GetResourceStatus(row); break;
                case "isPermanent": exportRow["Permanent"] = row.IsPermanent; break;
                case "backendResourceId": exportRow["Backend Resource ID"] = row.BackendResourceId; break;
                case "ip": exportRow["IP Address"] = row.Ip; break;
                case "shellPrefix": exportRow["Shell Prefix"] = row.ShellPrefix; break;
                case "cpuCores": exportRow["CPU cores"] = row.CpuCores; break;
                case "memoryBytes": exportRow["Memory bytes"] = row.MemoryBytes; break;
                case "maxProcesses": exportRow["Max processes"] = row.MaxProcesses; break;
                case "backendNetworkName": exportRow["Network"] = row.BackendNetworkName; break;
                case "createdAt": exportRow["Created"] = row.CreatedAt; break;
                case "updatedAt": exportRow["Updated"] = row.UpdatedAt; break;
                case "lastActiveAt": exportRow["Last active"] = row.LastActiveAt; break;
                case "stoppedAt": exportRow["Stopped"] = row.StoppedAt; break;
                case "deletedAt": exportRow["Deleted"] = row.DeletedAt; break;
                case "cleanupAt": exportRow["Cleanup"] = row.CleanupAt; break;
                case "volumeDeclaredBytes": exportRow["Volume bytes"] = row.VolumeDeclaredBytes; break;
                case "volumeMountCount": exportRow["Volume mounts"] = row.VolumeMountCount; break;
                case "chatAccessCount": exportRow["Chat access count"] = row.ChatAccessCount; break;
            }
        }

        return exportRow;
    }

    private static string GetResourceStatus(ContainerResourceAdminDto row)
        => row.DeletedAt is not null ? "Deleted" : row.StoppedAt is not null ? "Stopped" : "Running";

    private Task<RuntimeNodeDto?> GetRuntimeNodeDtoAsync(int id, CancellationToken cancellationToken)
        => _db.ContainerRuntimeNodes
            .Where(x => x.Id == id)
            .Select(x => new RuntimeNodeDto(x.Id, x.Name, x.AiName, x.Description, x.BackendType,
                x.Endpoint, x.Credential != null, x.IsEnabled, x.CreatedAt, x.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private static string? NormalizeImageName(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;
        string normalized = image.Trim();
        return normalized.Length > 512 ? null : normalized;
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
