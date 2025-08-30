using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Prompts;
using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chat/{encryptedChatId}/span"), Authorize]
public class ChatSpanController(ChatsDB db, IUrlEncryptionService idEncryption, CurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatSpanDto>> CreateChatSpan(string encryptedChatId, [FromBody] CreateChatSpanRequest request,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans.OrderByDescending(x => x.SpanId))
            .FirstOrDefaultAsync(x => x.Id == idEncryption.DecryptChatId(encryptedChatId) && x.UserId == currentUser.Id && !x.IsArchived, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        if (chat.ChatSpans.Count >= CreateChatSpanRequest.MaxSpanCount)
        {
            return BadRequest("Max span count reached");
        }

        UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id)
                .Where(x => x.ModelId == request.ModelId)
                .FirstOrDefaultAsync(cancellationToken);
        if (um == null)
        {
            return BadRequest("No models available");
        }

        PromptDto defaultPrompt = await PromptsController.GetDefaultPrompt(db, currentUser.Id, cancellationToken);
        ChatSpan toAdd = new()
        {
            ChatId = chat.Id,
            SpanId = CreateChatSpanRequest.FindAvailableSpanId([.. chat.ChatSpans.Select(x => x.SpanId)]),
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
                ImageSizeId = 0, // Default to 0 (DBKnownImageSize.Default)
            }
        };

        chat.ChatSpans.Add(toAdd);
        await db.SaveChangesAsync(cancellationToken);
        return Created(default(string), ChatSpanDto.FromDB(toAdd));
    }

    [HttpPost("{spanId:int}/enable")]
    public async Task<ActionResult> ToggleEnable(string encryptedChatId, byte spanId, CancellationToken cancellationToken)
    {
        return await ToggleEnableDisable(encryptedChatId, spanId, true, cancellationToken);
    }

    [HttpPost("{spanId:int}/disable")]
    public async Task<ActionResult> ToggleDisable(string encryptedChatId, byte spanId, CancellationToken cancellationToken)
    {
        return await ToggleEnableDisable(encryptedChatId, spanId, false, cancellationToken);
    }

    private async Task<ActionResult> ToggleEnableDisable(string encryptedChatId, byte spanId, bool enabled, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        ChatSpan? span = await db.ChatSpans.FirstOrDefaultAsync(x =>
            x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }
        span.Enabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{spanId:int}/switch-model/{modelId:int}")]
    public async Task<ActionResult<ChatSpanDto>> SwitchModel(string encryptedChatId, byte spanId, short modelId,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        ChatSpan? span = await db.ChatSpans
            .Include(x => x.ChatConfig)
            .FirstOrDefaultAsync(x => x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == modelId, cancellationToken);
        if (um == null)
        {
            return BadRequest("Model not available");
        }

        span.ChatConfig.Model = um.Model;
        span.ChatConfig.ModelId = um.ModelId;
        span.ChatConfig.WebSearchEnabled = um.Model.ModelReference.AllowSearch && span.ChatConfig.WebSearchEnabled;
        if (span.ChatConfig.Temperature != null)
        {
            span.ChatConfig.Temperature = (float)Math.Clamp((decimal)span.ChatConfig.Temperature.Value, um.Model.ModelReference.MinTemperature, um.Model.ModelReference.MaxTemperature);
        }
        if (span.ChatConfig.MaxOutputTokens != null)
        {
            span.ChatConfig.MaxOutputTokens = Math.Min(span.ChatConfig.MaxOutputTokens.Value, um.Model.ModelReference.MaxResponseTokens);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ChatSpanDto.FromDB(span));
    }

    [HttpPut("{spanId:int}")]
    public async Task<ActionResult<ChatSpanDto>> UpdateChatSpan(string encryptedChatId, byte spanId, [FromBody] UpdateChatSpanRequest request,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Mcps.Select(x => x.Id).Distinct().Count() != request.Mcps.Length)
        {
            return BadRequest("Duplicate MCP servers are not allowed");
        }

        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        ChatSpan? span = await db.ChatSpans
            .Include(x => x.ChatConfig)
                .ThenInclude(x => x.ChatConfigMcps)
            .FirstOrDefaultAsync(x => x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == request.ModelId, cancellationToken);
        if (um == null)
        {
            return BadRequest("Model not available");
        }

        request.ApplyTo(span);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        span.ChatConfig.Model = um.Model;
        return Ok(ChatSpanDto.FromDB(span));
    }

    [HttpDelete("{spanId}")]
    public async Task<IActionResult> DeleteChatSpan(string encryptedChatId, byte spanId, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        ChatSpan? span = await db.ChatSpans
            .Include(x => x.ChatConfig).ThenInclude(x => x.ChatSpans)
            .FirstOrDefaultAsync(x => x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        db.ChatSpans.Remove(span);
        if (span.ChatConfig.ChatSpans.Count == 1)
        {
            db.ChatConfigs.Remove(span.ChatConfig);
        }
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("apply-preset/{presetId}")]
    public async Task<ActionResult<ChatSpanDto[]>> ApplyPreset(string encryptedChatId, string presetId,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans).ThenInclude(x => x.ChatConfig).ThenInclude(x => x.ChatSpans)
            .FirstOrDefaultAsync(x => x.Id == chatId && x.UserId == currentUser.Id && !x.IsArchived, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        ChatPreset? preset = await db.ChatPresets
            .Include(x => x.ChatPresetSpans).ThenInclude(x => x.ChatConfig)
            .FirstOrDefaultAsync(x => x.Id == idEncryption.DecryptChatPresetId(presetId) && x.UserId == currentUser.Id, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        HashSet<short> requiredModelIds = [.. preset.ChatPresetSpans.Select(x => x.ChatConfig.ModelId)];
        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, requiredModelIds, cancellationToken);
        if (userModels.Count != requiredModelIds.Count)
        {
            return BadRequest("Not all models available");
        }

        Dictionary<byte, ChatSpan> dbSpans = chat.ChatSpans.ToDictionary(x => x.SpanId, v => v);
        Dictionary<byte, ChatPresetSpan> presetSpans = preset.ChatPresetSpans.ToDictionary(x => x.SpanId, v => v);
        HashSet<byte> allSpans = [.. preset.ChatPresetSpans.Select(x => x.SpanId), .. dbSpans.Keys];
        // Compare and update/insert/delete spans
        foreach (byte spanId in allSpans)
        {
            ChatSpan? existingSpan = dbSpans.GetValueOrDefault(spanId);
            ChatPresetSpan? toUpdate = presetSpans.GetValueOrDefault(spanId);

            if (existingSpan != null && toUpdate != null)
            {
                // update existing span
                toUpdate.ApplyTo(existingSpan, userModels[toUpdate.ChatConfig.ModelId].Model);
            }
            else if (existingSpan != null)
            {
                // delete existing span
                chat.ChatSpans.Remove(existingSpan);
                if (existingSpan.ChatConfig.ChatSpans.Count == 1)
                {
                    db.ChatConfigs.Remove(existingSpan.ChatConfig);
                }
            }
            else if (toUpdate != null)
            {
                // insert new span
                chat.ChatSpans.Add(toUpdate.ToChatSpan(userModels[toUpdate.ChatConfig.ModelId].Model, spanId));
            }
        }
        if (db.ChangeTracker.HasChanges())
        {
            chat.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        ChatSpanDto[] result = [.. chat.ChatSpans.Select(ChatSpanDto.FromDB)];
        return Ok(result);
    }
}
