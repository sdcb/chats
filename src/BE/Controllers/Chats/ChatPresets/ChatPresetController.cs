using Chats.BE.Controllers.Chats.ChatPresets.Dtos;
using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.Controllers.Chats.Prompts;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Chats.BE.DB.Enums;

namespace Chats.BE.Controllers.Chats.ChatPresets;

[Route("api/chat-preset"), Authorize]
public class ChatPresetController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChatPresetDto[]>> ListChatPreset(CancellationToken cancellationToken)
    {
        ChatPresetDto[] result = await db.ChatPresets
            .Where(x => x.UserId == currentUser.Id)
            .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
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
                    ImageSize = (DBKnownImageSize)x.ChatConfig.ImageSizeId,
                    Mcps = x.ChatConfig.ChatConfigMcps.Select(mcp => new ChatSpanMcp
                    {
                        Id = mcp.McpServerId,
                        CustomHeaders = mcp.CustomHeaders
                    }).ToArray()
                }).ToArray()
            })
            .ToArrayAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{presetId}/name")]
    public async Task<ActionResult<ChatPresetDto>> UpdateChatPresetName(string presetId, [FromBody] string name, CancellationToken cancellationToken)
    {
        ChatPreset? preset = await LoadOneChatPreset(presetId, cancellationToken);
        if (preset == null)
        {
            return NotFound();
        }

        preset.Name = name;
        if (db.ChangeTracker.HasChanges())
        {
            preset.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        ChatPresetDto result = ChatPresetDto.FromDB(preset, idEncryption);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ChatPresetDto>> CreateChatPreset([FromBody] UpdateChatPresetRequest req,
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

        if (req.Spans.Length == 0)
        {
            return BadRequest("At least one span is required");
        }

        HashSet<short> modelIds = [.. req.Spans.Select(x => x.ModelId)];

        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, modelIds, cancellationToken);
        if (userModels.Count != modelIds.Count)
        {
            return BadRequest("Model not available");
        }

        ChatPreset preset = new()
        {
            Name = req.Name,
            UserId = currentUser.Id,
            UpdatedAt = DateTime.UtcNow,
            ChatPresetSpans = [.. req.Spans.Select((x, i) => x.ToDB(userModels[x.ModelId].Model, (byte)i))],
        };
        db.ChatPresets.Add(preset);
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

        HashSet<short> modelIds = [.. req.Spans.Select(x => x.ModelId)];

        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, modelIds, cancellationToken);
        if (userModels.Count != modelIds.Count)
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
                preset.ChatPresetSpans.Remove(existingSpan);
                db.ChatConfigs.Remove(existingSpan.ChatConfig);
            }
            else if (toUpdateRaw != null)
            {
                // insert new span
                preset.ChatPresetSpans.Add(toUpdateRaw.ToDB(userModels[toUpdateRaw.ModelId].Model, spanId));
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
                    ImageSizeId = x.ChatConfig.ImageSizeId,
                    ChatConfigMcps = [.. x.ChatConfig.ChatConfigMcps.Select(mcp => new ChatConfigMcp
                    {
                        McpServerId = mcp.McpServerId,
                        CustomHeaders = mcp.CustomHeaders,
                    })],
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
                MaxOutputTokens = null,
                ReasoningEffort = 0,
                SystemPrompt = defaultPrompt.Content,
                ImageSizeId = (short)DBKnownImageSize.Default,
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

    [HttpPut("reorder")]
    public async Task<ActionResult> ReorderChatPresets([FromBody] EncryptedReorderRequest request, CancellationToken cancellationToken)
    {
        ReorderRequest<int> decryptedRequest = request.DecryptAsChatPreset(idEncryption);
        
        // 验证被移动的 ChatPreset 是否存在
        ChatPreset? sourcePreset = await db.ChatPresets
            .FirstOrDefaultAsync(x => x.Id == decryptedRequest.SourceId && x.UserId == currentUser.Id, cancellationToken);
        if (sourcePreset == null)
        {
            return NotFound("Source preset not found");
        }

        // 验证 previous 和 next 的 ChatPreset 是否存在（如果提供的话）
        ChatPreset? previousPreset = null;
        ChatPreset? nextPreset = null;

        if (decryptedRequest.PreviousId != null)
        {
            previousPreset = await db.ChatPresets
                .FirstOrDefaultAsync(x => x.Id == decryptedRequest.PreviousId && x.UserId == currentUser.Id, cancellationToken);
            if (previousPreset == null)
            {
                return NotFound("Previous preset not found");
            }
        }

        if (decryptedRequest.NextId != null)
        {
            nextPreset = await db.ChatPresets
                .FirstOrDefaultAsync(x => x.Id == decryptedRequest.NextId && x.UserId == currentUser.Id, cancellationToken);
            if (nextPreset == null)
            {
                return NotFound("Next preset not found");
            }
        }

        // 验证 previous 和 next 不能同时为空
        if (previousPreset == null && nextPreset == null)
        {
            return BadRequest("Both previous and next presets cannot be null");
        }

        // 验证 previous 和 next 的顺序（Order 是从小到大排列的）
        if (previousPreset != null && nextPreset != null && previousPreset.Order >= nextPreset.Order)
        {
            return BadRequest("Invalid order: previous preset should have smaller order than next preset");
        }

        // 尝试应用移动
        bool needReorder = !TryApplyMove(sourcePreset, previousPreset, nextPreset);
        
        if (needReorder)
        {
            // 需要重新排序，重排序当前用户的所有 ChatPresets
            ChatPreset[] allUserPresets = await db.ChatPresets
                .Where(x => x.UserId == currentUser.Id)
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderChatPresets(allUserPresets);
            
            // 重新加载并应用移动
            sourcePreset = allUserPresets.First(x => x.Id == decryptedRequest.SourceId);
            previousPreset = decryptedRequest.PreviousId != null ? allUserPresets.First(x => x.Id == decryptedRequest.PreviousId) : null;
            nextPreset = decryptedRequest.NextId != null ? allUserPresets.First(x => x.Id == decryptedRequest.NextId) : null;
            
            TryApplyMove(sourcePreset, previousPreset, nextPreset);
        }

        if (db.ChangeTracker.HasChanges())
        {
            sourcePreset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool TryApplyMove(ChatPreset sourcePreset, ChatPreset? previousPreset, ChatPreset? nextPreset)
    {
        return ReorderHelper.Default.TryApplyMove(sourcePreset, previousPreset, nextPreset);
    }

    private static void ReorderChatPresets(ChatPreset[] existingPresets)
    {
        ReorderHelper.Default.ReorderEntities(existingPresets);
    }

    async Task<ChatPreset?> LoadOneChatPreset(string presetId, CancellationToken cancellationToken)
    {
        return await db.ChatPresets
            .Include(x => x.ChatPresetSpans)
                .ThenInclude(x => x.ChatConfig)
                .ThenInclude(x => x.Model)
                .ThenInclude(x => x.ModelReference)
            .Include(x => x.ChatPresetSpans)
                .ThenInclude(x => x.ChatConfig)
                .ThenInclude(x => x.ChatConfigMcps)
            .Where(x => x.Id == idEncryption.DecryptChatPresetId(presetId) && x.UserId == currentUser.Id)
            .AsSingleQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
