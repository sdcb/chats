﻿using Chats.BE.Controllers.Admin.FileServices.Dtos;
using Chats.BE.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.FileServices;

[Route("api/admin/file-service")]
public class FileServiceController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FileServiceSimpleDto[]>> ListFileServices(bool select, CancellationToken cancellationToken)
    {
        if (select)
        {
            // simple mode, only return enabled id and name
            FileServiceSimpleDto[] data = await db.FileServices
                .Where(x => x.Enabled)
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
                .Select(x => new FileServiceDtoTemp
                {
                    Id = x.Id,
                    Name = x.Name,
                    Type = x.Type,
                    Configs = x.Configs,
                    Enabled = x.Enabled,
                    CreatedAt = x.CreatedAt,
                })
                .AsEnumerable()
                .Select(x => x.ToDto().WithMaskedKeys())
                .ToArray();
            return Ok(data);
        }
    }
}