using Chats.DB;
using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.DB.Init;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Prompts;

[Route("api/prompts"), Authorize]
public class PromptsController(ChatsDB db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PromptDto[]>> GetPrompts(CancellationToken cancellationToken)
    {
        PromptDto[] prompts = await db.Prompts
            .Where(x => x.CreateUserId == currentUser.Id || currentUser.IsAdmin && x.IsSystem)
            .OrderBy(x => x.IsSystem)
            .ThenBy(x => x.IsDefault)
            .ThenBy(x => x.UpdatedAt)
            .Select(x => new PromptDto()
            {
                Content = x.Content,
                Temperature = x.Temperature,
                Id = x.Id,
                Name = x.Name,
                IsDefault = x.IsDefault,
                UpdatedAt = x.UpdatedAt,
                IsSystem = x.IsSystem
            })
            .ToArrayAsync(cancellationToken);
        return Ok(prompts);
    }

    [HttpGet("brief")]
    public async Task<ActionResult<BriefPromptDto[]>> GetBriefPrompts(CancellationToken cancellationToken)
    {
        BriefPromptDto[] prompts = await db.Prompts
            .Where(x => x.CreateUserId == currentUser.Id || currentUser.IsAdmin && x.IsSystem)
            .OrderBy(x => x.IsSystem)
            .ThenBy(x => x.IsDefault)
            .ThenBy(x => x.UpdatedAt)
            .Select(x => new BriefPromptDto()
            {
                Id = x.Id,
                Name = x.Name,
                IsDefault = x.IsDefault,
                IsSystem = x.IsSystem,
                UpdatedAt = x.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
        return Ok(prompts);
    }

    [HttpGet("{promptId:int}")]
    public async Task<ActionResult<PromptDto>> GetSinglePrompt(int promptId, CancellationToken cancellationToken)
    {
        Prompt? prompt = await db.Prompts.FirstOrDefaultAsync(x => x.Id == promptId && (x.IsSystem || x.CreateUserId == currentUser.Id), cancellationToken);
        if (prompt == null)
        {
            return NotFound();
        }
        return Ok(new PromptDto()
        {
            Content = prompt.Content,
            Temperature = prompt.Temperature,
            Id = prompt.Id,
            Name = prompt.Name,
            IsDefault = prompt.IsDefault,
            UpdatedAt = prompt.UpdatedAt,
            IsSystem = prompt.IsSystem,
        });
    }

    [HttpGet("default")]
    public async Task<ActionResult<PromptDto>> GetDefaultPrompt(CancellationToken cancellationToken)
    {
        PromptDto dto = await GetDefaultPrompt(db, currentUser.Id, cancellationToken);
        return Ok(dto);
    }

    internal static async Task<PromptDto> GetDefaultPrompt(ChatsDB db, int userId, CancellationToken cancellationToken)
    {
        Prompt? userDefault = await db.Prompts
                .OrderByDescending(x => x.UpdatedAt)
                .Where(x => !x.IsSystem && x.IsDefault && x.CreateUserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
        Prompt? systemDefault = await db.Prompts
                .OrderByDescending(x => x.UpdatedAt)
                .Where(x => x.IsSystem)
                .FirstOrDefaultAsync(cancellationToken);
        Prompt? consolidated = userDefault ?? systemDefault;

        PromptDto dto = consolidated != null ? new PromptDto()
        {
            Content = consolidated.Content,
            Temperature = consolidated.Temperature,
            Id = consolidated.Id,
            Name = consolidated.Name,
            IsDefault = consolidated.IsDefault,
            UpdatedAt = consolidated.UpdatedAt,
            IsSystem = consolidated.IsSystem,
        } : new PromptDto()
        {
            Content = InitService.DefaultPrompt,
            Temperature = null,
            Id = -1,
            IsDefault = true,
            Name = "Default",
            UpdatedAt = DateTime.UtcNow,
            IsSystem = true
        };

        return dto;
    }

    [HttpPost]
    public async Task<ActionResult<BriefPromptDto>> CreatePrompt([FromBody] CreatePromptDto request, CancellationToken cancellationToken)
    {
        Prompt prompt = request.ToPrompt(currentUser.Id, currentUser.IsAdmin);

        db.Prompts.Add(prompt);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new BriefPromptDto()
        {
            Id = prompt.Id,
            IsDefault = prompt.IsDefault,
            Name = prompt.Name,
            UpdatedAt = prompt.UpdatedAt,
            IsSystem = prompt.IsSystem
        });
    }

    [HttpDelete]
    public async Task<ActionResult> DeletePrompt([FromQuery] int id, CancellationToken cancellationToken)
    {
        Prompt? prompt = await db.Prompts.FirstOrDefaultAsync(x => x.Id == id && (x.CreateUserId == currentUser.Id || currentUser.IsAdmin), cancellationToken);
        if (prompt == null)
        {
            return NotFound();
        }
        db.Prompts.Remove(prompt);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{promptId:int}")]
    public async Task<ActionResult<PromptDto>> UpdatePrompt(int promptId, [FromBody] CreatePromptDto request, CancellationToken cancellationToken)
    {
        Prompt? prompt = await db.Prompts.FirstOrDefaultAsync(x => x.Id == promptId && (x.CreateUserId == currentUser.Id || currentUser.IsAdmin), cancellationToken);
        if (prompt == null)
        {
            return NotFound();
        }

        request.ApplyTo(prompt, currentUser.IsAdmin);
        if (db.ChangeTracker.HasChanges())
        {
            prompt.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(request);
    }
}
