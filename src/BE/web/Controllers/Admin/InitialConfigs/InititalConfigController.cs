using Chats.DB;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.InitialConfigs.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.InitialConfigs;

[Route("api/admin/user-config"), AuthorizeAdmin]
public class InititalConfigController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public UserInitialConfigDto[] GetUserInitialConfigs()
    {
        UserInitialConfigDto[] data = db.UserInitialConfigs
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new UserInitialConfigDtoTemp()
            {
                Id = x.Id,
                Name = x.Name,
                LoginType = x.LoginType ?? "-",
                Models = x.Models,
                Price = x.Price,
                InvitationCodeId = x.InvitationCodeId,
                InvitationCode = x.InvitationCode!.Value ?? "-",
                Mcps = x.Mcps,
                ApiKeyEnabled = x.ApiKeyEnabled,
            })
            .AsEnumerable()
            .Select(x => x.ToDto())
            .ToArray();
        return data;
    }

    [HttpPut]
    public async Task<ActionResult> UpdateInitialConfig([FromBody] UserInitialConfigUpdateRequest req, CancellationToken cancellationToken)
    {
        UserInitialConfig? existingConfig = await db.UserInitialConfigs.FindAsync([req.Id], cancellationToken);
        if (existingConfig == null)
        {
            return NotFound();
        }

        string? validationError = await ValidateMcps(req, cancellationToken);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        req.ApplyTo(existingConfig);
        if (db.ChangeTracker.HasChanges())
        {
            existingConfig.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteInitialConfig(int id, CancellationToken cancellationToken)
    {
        UserInitialConfig? existingConfig = await db.UserInitialConfigs.FindAsync([id], cancellationToken);
        if (existingConfig == null)
        {
            return NotFound();
        }

        db.UserInitialConfigs.Remove(existingConfig);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateInitialConfig([FromBody] UserInitialConfigCreateRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string? validationError = await ValidateMcps(req, cancellationToken);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        UserInitialConfig newOne = new()
        {
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow, 
        };

        req.ApplyTo(newOne);
        await db.UserInitialConfigs.AddAsync(newOne, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    private async Task<string?> ValidateMcps(UserInitialConfigRequest req, CancellationToken cancellationToken)
    {
        if (req.Mcps.Any(x => x.McpServerId <= 0))
        {
            return "MCP server ID must be positive";
        }

        int[] mcpServerIds = [.. req.Mcps.Select(x => x.McpServerId).Distinct()];
        if (mcpServerIds.Length != req.Mcps.Length)
        {
            return "MCP server IDs must be unique";
        }

        if (req.Mcps.Any(x => !x.HasValidCustomHeaders()))
        {
            return "MCP custom headers must be empty or a valid JSON object";
        }

        int existingCount = await db.McpServers
            .CountAsync(x => mcpServerIds.Contains(x.Id), cancellationToken);
        return existingCount == mcpServerIds.Length
            ? null
            : "One or more MCP servers do not exist";
    }
}
