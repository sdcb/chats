using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.FileServices.Dtos;
using Chats.BE.Services.FileServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Chats.BE.Controllers.Admin.FileServices;

[Route("api/admin/file-service"), AuthorizeAdmin]
public class FileServiceController(ChatsDB db, IFileServiceFactory fileServiceFactory) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateFileService(
        [FromBody] FileServiceUpdateRequest req,
        [FromServices] ILogger<FileServiceController> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Requirement: put (upload) an object using the configured file service, then try delete it.
            // Upload success => validate success. Delete result is not important.
            FileService temp = new()
            {
                // not persisted, only used to create service implementation
                FileServiceTypeId = (byte)req.FileServiceTypeId,
                Name = req.Name,
                Configs = req.Configs,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            IFileService fileService = fileServiceFactory.Create(temp);

            string now = DateTime.UtcNow.ToString("O");
            byte[] bytes = Encoding.UTF8.GetBytes($"validate at {now}");
            await using MemoryStream ms = new(bytes);
            string fileName = $"file-service-validate-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt";
            string storageKey = await fileService.Upload(new FileUploadRequest
            {
                FileName = fileName,
                ContentType = "text/plain",
                Stream = ms,
            }, cancellationToken);

            try
            {
                _ = await fileService.Delete(storageKey, cancellationToken);
            }
            catch
            {
                // ignore delete failure
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating file service");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<FileServiceSimpleDto[]>> ListFileServices(bool select, CancellationToken cancellationToken)
    {
        if (select)
        {
            // simple mode, only return enabled id and name
            FileServiceSimpleDto[] data = await db.FileServices
                .Select(x => new FileServiceSimpleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                })
                .ToArrayAsync(cancellationToken);
            return Ok(data);
        }
        else
        {
            // full mode, return all fields
            FileServiceDto[] data = await db.FileServices
                .Select(x => new FileServiceDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    FileServiceTypeId = (DBFileServiceType)x.FileServiceTypeId,
                    Configs = x.Configs,
                    IsDefault = x.IsDefault,
                    CreatedAt = x.CreatedAt,
                    FileCount = x.Files.Count,
                    UpdatedAt = x.UpdatedAt,
                })
                .ToArrayAsync(cancellationToken);
            return Ok(data);
        }
    }

    [HttpPut("{fileServiceId:int}")]
    public async Task<ActionResult> UpdateFileService(int fileServiceId, [FromBody] FileServiceUpdateRequest req, CancellationToken cancellationToken)
    {
        FileService? existingData = await db.FileServices.FindAsync([fileServiceId], cancellationToken);
        if (existingData == null)
        {
            return NotFound();
        }

        req.ApplyTo(existingData);
        if (db.ChangeTracker.HasChanges())
        {
            existingData.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (existingData.IsDefault)
            {
                await db.FileServices
                    .Where(x => x.Id != existingData.Id && x.IsDefault)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, false), cancellationToken);
            }
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> CreateFileService([FromBody] FileServiceUpdateRequest req, CancellationToken cancellationToken)
    {
        FileService toInsert = new()
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        req.ApplyTo(toInsert);
        db.FileServices.Add(toInsert);
        await db.SaveChangesAsync(cancellationToken);

        if (toInsert.IsDefault)
        {
            await db.FileServices
                .Where(x => x.Id != toInsert.Id && x.IsDefault)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, false), cancellationToken);
        }
        return Created(default(string), value: toInsert.Id);
    }

    [HttpDelete("{fileServiceId:int}")]
    public async Task<ActionResult> DeleteFileService(int fileServiceId, CancellationToken cancellationToken)
    {
        FileService? existingData = await db.FileServices.FindAsync([fileServiceId], cancellationToken);
        if (existingData == null)
        {
            return NotFound();
        }

        if (await db.Files.AnyAsync(x => x.FileServiceId == fileServiceId, cancellationToken))
        {
            return BadRequest("Cannot delete file service with existing files");
        }

        db.FileServices.Remove(existingData);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
