using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.Configs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

[Route("api/admin/global-configs"), AuthorizeAdmin]
public class GlobalConfigController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public async Task<GlobalConfigDto[]> GetGlobalConfigs(CancellationToken cancellationToken)
    {
        GlobalConfigDto[] data = await db.Configs
            .Select(x => new GlobalConfigDto()
            {
                Key = x.Key,
                Value = x.Value,
                Description = x.Description,
            })
            .ToArrayAsync(cancellationToken);
        return data;
    }

    [HttpPut]
    public async Task<ActionResult> UpdateGlobalConfig([FromBody] GlobalConfigDto req, CancellationToken cancellationToken)
    {
        Config? config = await db.Configs.FindAsync([req.Key], cancellationToken);
        if (config == null)
        {
            return NotFound();
        }

        // ensure value is valid json
        try
        {
            JsonDocument.Parse(req.Value);
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON");
        }

        config.Value = req.Value;
        config.Description = req.Description;
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> CreateGlobalConfig([FromBody] GlobalConfigDto req, CancellationToken cancellationToken)
    {
        Config? config = await db.Configs.FindAsync([req.Key], cancellationToken);
        if (config != null)
        {
            return BadRequest("Key already exists");
        }
        // ensure value is valid json
        try
        {
            JsonDocument.Parse(req.Value);
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON");
        }
        await db.Configs.AddAsync(new Config()
        {
            Key = req.Key,
            Value = req.Value,
            Description = req.Description,
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
