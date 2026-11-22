using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Containers;
using Chats.DB;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Controllers.Users.Containers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;

namespace Chats.BE.Controllers.Users.Containers;

[ApiController]
[Authorize]
[Route("api/containers")]
public sealed class ContainerResourceController(
    ChatsDB db,
    CurrentUser currentUser,
    ContainerResourceService resources,
    IUrlEncryptionService encryption) : ControllerBase
{
    private const int ResourceExportLimit = 10000;
    private readonly ChatsDB _db = db;
    private readonly CurrentUser _currentUser = currentUser;
    private readonly ContainerResourceService _resources = resources;
    private readonly IUrlEncryptionService _encryption = encryption;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContainerResourceDto>>> List([FromQuery] bool includeDeleted, CancellationToken cancellationToken)
    {
        IReadOnlyList<ContainerResource> resources = await _resources.ListMineAsync(includeDeleted, cancellationToken);
        return resources.Select(ToDto).ToArray();
    }

    [HttpGet("resources")]
    public async Task<ActionResult<PagedResult<UserContainerResourceDto>>> Resources(
        [FromQuery] UserContainerResourceQuery query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        IQueryable<UserContainerResourceRow> rows = BuildResourceQuery(query)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(ProjectResource());
        return Ok(await PagedResult.FromTempQuery(
            rows,
            query,
            ToDto,
            cancellationToken));
    }

    [HttpGet("resources/export")]
    public async Task<IActionResult> ExportResources(
        [FromQuery] UserContainerResourceExportQuery query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        UserContainerResourceRow[] rows = await BuildResourceQuery(query)
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

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ContainerTemplateDto>>> Templates(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContainerResourceTemplate> templates = await _resources.ListTemplatesAsync(false, cancellationToken);
        return templates
            .Select(x => new ContainerTemplateDto(x.Id, x.Name, x.RuntimeNodeId, x.RuntimeNode.AiName, x.Image, x.CpuCores, x.MemoryBytes, x.MaxProcesses, x.BackendNetworkName, x.DefaultVolumeBytes, x.Visibility))
            .ToArray();
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

    private IQueryable<ContainerResource> BuildResourceQuery(
        UserContainerResourceQuery query)
        => BuildResourceQuery(
            query.Id,
            query.Query,
            query.RuntimeNodeId,
            query.Status,
            query.Permanent);

    private IQueryable<ContainerResource> BuildResourceQuery(
        UserContainerResourceExportQuery query)
        => BuildResourceQuery(
            query.Id,
            query.Query,
            query.RuntimeNodeId,
            query.Status,
            query.Permanent);

    private IQueryable<ContainerResource> BuildResourceQuery(
        string? id,
        string? search,
        int? runtimeNodeId,
        string? status,
        bool? permanent)
    {
        IQueryable<ContainerResource> rows = _db.ContainerResources
            .AsNoTracking()
            .Where(x => x.OwnerUserId == _currentUser.Id);

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

    private static System.Linq.Expressions.Expression<Func<ContainerResource, UserContainerResourceRow>> ProjectResource()
        => x => new UserContainerResourceRow(
            x.Id,
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

    private UserContainerResourceDto ToDto(UserContainerResourceRow row) => new(
        row.Id,
        _encryption.Encrypt(row.Id, EncryptionPurpose.DockerSessionId),
        row.OwnerChatId,
        row.OwnerChatTitle,
        row.OwnerTurnId,
        row.RuntimeNodeId,
        row.RuntimeNodeName,
        row.RuntimeNodeAIName,
        row.IsPermanent,
        row.BackendResourceId,
        row.Ip,
        row.Name,
        row.Image,
        row.ShellPrefix,
        row.CpuCores,
        row.MemoryBytes,
        row.MaxProcesses,
        row.BackendNetworkName,
        row.CreatedAt,
        row.UpdatedAt,
        row.LastActiveAt,
        row.StoppedAt,
        row.DeletedAt,
        row.CleanupAt,
        row.VolumeDeclaredBytes,
        row.VolumeMountCount,
        row.ChatAccessCount);

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
        UserContainerResourceRow row,
        IReadOnlyCollection<string>? selectedColumns)
    {
        IEnumerable<string> columns = selectedColumns ??
        [
            "id", "runtimeNodeId", "runtimeNodeName", "runtimeNodeAIName", "name", "image", "status",
            "isPermanent", "backendResourceId", "ip", "cpuCores", "memoryBytes", "maxProcesses",
            "backendNetworkName", "createdAt", "updatedAt", "lastActiveAt", "stoppedAt", "deletedAt",
            "cleanupAt", "volumeDeclaredBytes", "volumeMountCount", "chatAccessCount"
        ];
        Dictionary<string, object?> exportRow = new();
        foreach (string column in columns)
        {
            switch (column)
            {
                case "id": exportRow["ID"] = row.Id; break;
                case "ownerChatId": exportRow["Owner Chat ID"] = row.OwnerChatId; break;
                case "ownerChatTitle": exportRow["Owner Chat Title"] = row.OwnerChatTitle; break;
                case "ownerTurnId": exportRow["Owner Turn ID"] = row.OwnerTurnId; break;
                case "runtimeNodeId": exportRow["Runtime Node ID"] = row.RuntimeNodeId; break;
                case "runtimeNodeName": exportRow["Runtime node"] = row.RuntimeNodeName; break;
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

    private static string GetResourceStatus(UserContainerResourceRow row)
        => row.DeletedAt is not null ? "Deleted" : row.StoppedAt is not null ? "Stopped" : "Running";

    private sealed record UserContainerResourceRow(
        long Id,
        int? OwnerChatId,
        string? OwnerChatTitle,
        long? OwnerTurnId,
        int RuntimeNodeId,
        string? RuntimeNodeName,
        string? RuntimeNodeAIName,
        bool IsPermanent,
        string BackendResourceId,
        string? Ip,
        string Name,
        string Image,
        string? ShellPrefix,
        float? CpuCores,
        long? MemoryBytes,
        int? MaxProcesses,
        string? BackendNetworkName,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? LastActiveAt,
        DateTime? StoppedAt,
        DateTime? DeletedAt,
        DateTime? CleanupAt,
        long? VolumeDeclaredBytes,
        int VolumeMountCount,
        int ChatAccessCount);

    private ContainerResourceDto ToDto(ContainerResource resource) => new(
        _encryption.Encrypt(resource.Id, EncryptionPurpose.DockerSessionId),
        resource.Name,
        resource.IsPermanent,
        resource.Image,
        resource.CpuCores,
        resource.MemoryBytes,
        resource.MaxProcesses,
        resource.BackendNetworkName,
        resource.RuntimeNode?.AiName,
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
