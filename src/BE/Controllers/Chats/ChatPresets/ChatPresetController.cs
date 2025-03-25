using Chats.BE.Controllers.Chats.ChatPresets.Dtos;
using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.Controllers.Chats.Prompts;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.ChatPresets;

[Route("api/chat-preset")]
public class ChatPresetController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChatPresetDto[]>> ListChatPreset(CancellationToken cancellationToken)
    {
        ChatPresetDto[] result = await db.ChatPresets
            .Where(x => x.UserId == currentUser.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new ChatPresetDto
            {
                Id = idEncryption.EncryptChatPresetId(x.Id),
                Name = x.Name,
                UpdatedAt = x.UpdatedAt,
                Spans = x.ChatPresetSpans.Select(x => new ChatSpanDto()
                {
                    SpanId = x.SpanId,
                    Enabled = x.Enabled,
                    SystemPrompt = x.ChatConfig.SystemPrompt,
                    ModelId = x.ChatConfig.ModelId,
                    ModelName = x.ChatConfig.Model.Name,
                    ModelProviderId = x.ChatConfig.Model.ModelReference.ProviderId,
                    Temperature = x.ChatConfig.Temperature,
                    WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
                    MaxOutputTokens = x.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = x.ChatConfig.ReasoningEffort,
                }).ToArray()
            })
            .ToArrayAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ChatPresetDto>> CreateChatPreset([FromBody] CreateChatPresetRequest request, CancellationToken cancellationToken)
    {
        ChatPreset preset = new()
        {
            Name = request.Name,
            UserId = currentUser.Id,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChatPresets.Add(preset);
        await db.SaveChangesAsync(cancellationToken);
        ChatPresetDto result = ChatPresetDto.FromDB(preset, idEncryption);
        return Ok(result);
    }

    [HttpPut("{presetId}/name")]
    public async Task<ActionResult<ChatPresetDto>> UpdateChatPresetName(string presetId, [FromBody] string name, CancellationToken cancellationToken)
    {
        ChatPreset? preset = await LoadOneChatPreset(presetId, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        preset.Name = name;
        preset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatPresetDto result = ChatPresetDto.FromDB(preset, idEncryption);
        return Ok(result);
    }

    [HttpPut("{presetId}")]
    public async Task<ActionResult<ChatPresetDto>> UpdateChatPreset(string presetId, [FromBody] UpdateChatPresetRequest req,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (req.Spans.Length >= CreateChatSpanRequest.MaxSpanCount)
        {
            return BadRequest("Max span count reached");
        }

        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, [.. req.Spans.Select(x => x.ModelId)], cancellationToken);
        if (userModels.Count != req.Spans.Length)
        {
            return BadRequest("Model not available");
        }

        ChatPreset? preset = await LoadOneChatPreset(presetId, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        preset.Name = req.Name;
        Dictionary<byte, ChatPresetSpan> dbSpans = preset.ChatPresetSpans.ToDictionary(x => x.SpanId, v => v);
        HashSet<byte> spanIds = [.. Enumerable.Range(0, req.Spans.Length).Select(x => (byte)x), .. dbSpans.Keys];
        // Compare and update/insert/delete spans
        foreach (byte spanId in spanIds)
        {
            ChatPresetSpan? existingSpan = dbSpans.GetValueOrDefault(spanId);
            UpdateChatSpanRequest? toUpdateRaw = req.Spans.ElementAtOrDefault(spanId);

            if (existingSpan != null && toUpdateRaw != null)
            {
                // update existing span
                toUpdateRaw.ApplyTo(existingSpan, userModels[toUpdateRaw.ModelId].Model);
            }
            else if (existingSpan != null)
            {
                // delete existing span
                db.ChatPresetSpans.Remove(existingSpan);
            }
            else if (toUpdateRaw != null)
            {
                // insert new span
                preset.ChatPresetSpans.Add(toUpdateRaw.ToDB(userModels[toUpdateRaw.ModelId].Model));
            }
        }
        if (db.ChangeTracker.HasChanges())
        {
            preset.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);

        ChatPresetDto result = ChatPresetDto.FromDB(preset, idEncryption);
        return Ok(result);
    }

    [HttpDelete("{presetId}")]
    public async Task<ActionResult> DeleteChatPreset(string presetId, CancellationToken cancellationToken)
    {
        ChatPreset? preset = await LoadOneChatPreset(presetId, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        // don't need to remove spans, they will be removed by cascade
        db.ChatPresets.Remove(preset);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("{presetId}/clone")]
    public async Task<ActionResult<ChatPresetDto>> ClonePreset(string presetId, CancellationToken cancellationToken)
    {
        ChatPreset? existingOne = await LoadOneChatPreset(presetId, cancellationToken);
        if (existingOne == null)
        {
            return NotFound();
        }

        ChatPreset newPreset = new()
        {
            Name = existingOne.Name,
            UserId = currentUser.Id,
            UpdatedAt = DateTime.UtcNow,
            ChatPresetSpans = [.. existingOne.ChatPresetSpans.Select(x => new ChatPresetSpan
            {
                SpanId = x.SpanId,
                Enabled = x.Enabled,
                ChatConfig = new ChatConfig
                {
                    SystemPrompt = x.ChatConfig.SystemPrompt,
                    ModelId = x.ChatConfig.ModelId,
                    Temperature = x.ChatConfig.Temperature,
                    WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
                    MaxOutputTokens = x.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = x.ChatConfig.ReasoningEffort,
                }
            })]
        };
        db.ChatPresets.Add(newPreset);
        await db.SaveChangesAsync(cancellationToken);
        ChatPresetDto result = ChatPresetDto.FromDB(newPreset, idEncryption);
        return Ok(result);
    }

    [HttpPost("{presetId}/span")]
    public async Task<ActionResult<ChatSpanDto>> CreatePresetSpan(string presetId, [FromBody] CreateChatSpanRequest dto,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        ChatPreset? preset = await LoadOneChatPreset(presetId, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        if (preset.ChatPresetSpans.Count >= CreateChatSpanRequest.MaxSpanCount)
        {
            return BadRequest("Max span count reached");
        }

        // check user model existance
        UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == dto.ModelId, cancellationToken);
        if (um == null)
        {
            return BadRequest("Model not available");
        }

        PromptDto defaultPrompt = await PromptsController.GetDefaultPrompt(db, currentUser.Id, cancellationToken);
        ChatPresetSpan span = new()
        {
            SpanId = CreateChatSpanRequest.FindAvailableSpanId([.. preset.ChatPresetSpans.Select(x => x.SpanId)]),
            Enabled = true,
            ChatConfig = new ChatConfig
            {
                ModelId = um.ModelId,
                Model = um.Model,
                Temperature = null,
                WebSearchEnabled = false,
                HashCode = 0,
                MaxOutputTokens = null,
                ReasoningEffort = 0,
                SystemPrompt = defaultPrompt.Content,
            }
        };
        preset.ChatPresetSpans.Add(span);
        preset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatSpanDto response = ChatSpanDto.FromDB(span);
        return Created(default(string), response);
    }

    [HttpPut("{presetId}/span/{spanId:int}")]
    public async Task<ActionResult<ChatSpanDto>> UpdatePresetSpan(string presetId, byte spanId, [FromBody] UpdateChatSpanRequest dto,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        ChatPresetSpan? span = await db.ChatPresetSpans
            .Include(x => x.ChatConfig)
                .ThenInclude(x => x.Model)
                .ThenInclude(x => x.ModelReference)
            .Where(x => x.ChatPresetId == idEncryption.DecryptChatPresetId(presetId) && x.SpanId == spanId && x.ChatPreset.UserId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        // check user model existance
        UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == dto.ModelId, cancellationToken);
        if (um == null)
        {
            return BadRequest("Model not available");
        }

        dto.ApplyTo(span, um.Model);
        span.ChatPreset.UpdatedAt = DateTime.UtcNow;
        if (db.ChangeTracker.HasChanges())
        {
            span.ChatPreset.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        ChatSpanDto response = ChatSpanDto.FromDB(span);
        return Ok(response);
    }

    [HttpDelete("{presetId}/span/{spanId:int}")]
    public async Task<ActionResult> DeletePresetSpan(string presetId, byte spanId, CancellationToken cancellationToken)
    {
        ChatPresetSpan? span = await db.ChatPresetSpans
            .Include(x => x.ChatPreset)
            .Where(x => x.ChatPresetId == idEncryption.DecryptChatPresetId(presetId) && x.SpanId == spanId && x.ChatPreset.UserId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (span == null)
        {
            return NotFound();
        }
        db.ChatPresetSpans.Remove(span);
        span.ChatPreset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    async Task<ChatPreset?> LoadOneChatPreset(string presetId, CancellationToken cancellationToken)
    {
        return await db.ChatPresets
            .Include(x => x.ChatPresetSpans)
                .ThenInclude(x => x.ChatConfig)
                .ThenInclude(x => x.Model)
                .ThenInclude(x => x.ModelReference)
            .Where(x => x.Id == idEncryption.DecryptChatPresetId(presetId) && x.UserId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
