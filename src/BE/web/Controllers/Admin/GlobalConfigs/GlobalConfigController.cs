using Chats.DB;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;
using Chats.BE.Services.Configs;
using Chats.BE.Services.TitleSummary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

[Route("api/admin/global-configs"), AuthorizeAdmin]
public class GlobalConfigController(
    ChatsDB db,
    IRequestTraceConfigProvider requestTraceConfigProvider,
    TitleSummaryConfigService titleSummaryConfigService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<GlobalConfigDto?>> GetGlobalConfig([FromRoute] string id, CancellationToken cancellationToken)
    {
        GlobalConfigDto? data = await db.Configs
            .Where(x => x.Key == id)
            .Select(x => new GlobalConfigDto()
            {
                Key = x.Key,
                Value = x.Value,
                Description = x.Description,
            })
            .SingleOrDefaultAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("title-summary")]
    public async Task<ActionResult<TitleSummaryAdminSettingsDto>> GetTitleSummarySettings(CancellationToken cancellationToken)
    {
        TitleSummaryConfig? config = await titleSummaryConfigService.GetAdminConfig(cancellationToken);
        return Ok(new TitleSummaryAdminSettingsDto
        {
            Config = config,
            DefaultPromptTemplate = TitleSummaryConfigService.DefaultPromptTemplate,
        });
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
            if (req.Key == DBConfigKey.InboundRequestTrace || req.Key == DBConfigKey.OutboundRequestTrace)
            {
                await requestTraceConfigProvider.ForceRefreshAsync(cancellationToken);
            }
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
        if (id == DBConfigKey.InboundRequestTrace || id == DBConfigKey.OutboundRequestTrace)
        {
            await requestTraceConfigProvider.ForceRefreshAsync(cancellationToken);
        }
        return NoContent();
    }
}
