using Chats.DB;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;
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
            config = new Config()
            {
                Key = req.Key,
            };
            db.Configs.Add(config);
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

    [HttpDelete]
    public async Task<ActionResult> DeleteGlobalConfig([FromQuery] string id, CancellationToken cancellationToken)
    {
        Config? config = await db.Configs.FindAsync([id], cancellationToken);
        if (config == null)
        {
            return NotFound();
        }
        db.Configs.Remove(config);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
