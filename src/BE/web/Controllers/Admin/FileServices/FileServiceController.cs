using Chats.Web.Controllers.Admin.Common;
using Chats.Web.Controllers.Admin.FileServices.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Controllers.Admin.FileServices;

[Route("api/admin/file-service"), AuthorizeAdmin]
public class FileServiceController(ChatsDB db) : ControllerBase
{
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
            FileServiceDto[] data = db.FileServices
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
                .AsEnumerable()
                .Select(x => x.WithMaskedKeys())
                .ToArray();
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
