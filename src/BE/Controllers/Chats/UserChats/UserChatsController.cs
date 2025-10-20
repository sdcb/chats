using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.Controllers.Chats.Prompts;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.BE.DB.Enums;

namespace Chats.BE.Controllers.Chats.UserChats;

[Route("api/user/chats"), Authorize]
public class UserChatsController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption) : ControllerBase
{
    [HttpGet("{encryptedChatId}")]
    public async Task<ActionResult<ChatsResponse>> GetOneChat(string encryptedChatId, CancellationToken cancellationToken)
    {
        ChatsResponse? result = await db.Chats
            .Where(x => x.Id == idEncryption.DecryptChatId(encryptedChatId) && x.UserId == currentUser.Id && !x.IsArchived)
            .Select(x => new ChatsResponse()
            {
                Id = idEncryption.EncryptChatId(x.Id),
                Title = x.Title,
                IsTopMost = x.IsTopMost,
                IsShared = x.ChatShares.Any(),
                GroupId = idEncryption.EncryptChatGroupId(x.ChatGroupId),
                Tags = x.ChatTags.Select(x => x.Name).ToArray(),
                Spans = x.ChatSpans.Select(span => new ChatSpanDto
                {
                    SpanId = span.SpanId,
                    Enabled = span.Enabled,
                    SystemPrompt = span.ChatConfig.SystemPrompt,
                    ModelId = span.ChatConfig.ModelId,
                    ModelName = span.ChatConfig.Model.Name,
                    ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
                    Temperature = span.ChatConfig.Temperature,
                    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
                    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                    ImageSize = (DBKnownImageSize)span.ChatConfig.ImageSizeId,
                    Mcps = span.ChatConfig.ChatConfigMcps
                        .Select(x => new ChatSpanMcp { Id = x.McpServerId, CustomHeaders = x.CustomHeaders })
                        .ToArray()
                }).ToArray(),
                LeafTurnId = idEncryption.EncryptTurnId(x.LeafTurnId),
                UpdatedAt = x.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ChatsResponse>>> GetChatsForGroup([FromQuery] ChatsQueryDto request, CancellationToken cancellationToken)
    {
        PagedResult<ChatsResponse> result = await GetChatsForGroupAsync(db, currentUser, idEncryption, request.ToChatsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("archived")]
    public async Task<ActionResult<PagedResult<ChatsResponse>>> ListArchived(QueryPagingRequest request, CancellationToken cancellationToken)
    {
        PagedResult<ChatsResponse> result = await PagedResult.FromQuery(db.Chats
            .Where(x => x.UserId == currentUser.Id && x.IsArchived)
            .Select(x => new ChatsResponse()
            {
                Id = idEncryption.EncryptChatId(x.Id),
                Title = x.Title,
                IsTopMost = x.IsTopMost,
                IsShared = x.ChatShares.Count != 0,
                GroupId = idEncryption.EncryptChatGroupId(x.ChatGroupId),
                Tags = x.ChatTags.Select(x => x.Name).ToArray(),
                Spans = x.ChatSpans.Select(span => new ChatSpanDto
                {
                    SpanId = span.SpanId,
                    Enabled = span.Enabled,
                    SystemPrompt = span.ChatConfig.SystemPrompt,
                    ModelId = span.ChatConfig.ModelId,
                    ModelName = span.ChatConfig.Model.Name,
                    ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
                    Temperature = span.ChatConfig.Temperature,
                    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
                    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                    ImageSize = (DBKnownImageSize)span.ChatConfig.ImageSizeId,
                    Mcps = span.ChatConfig.ChatConfigMcps.Select(x => new ChatSpanMcp { Id = x.McpServerId, CustomHeaders = x.CustomHeaders }).ToArray(),
                }).ToArray(),
                LeafTurnId = idEncryption.EncryptTurnId(x.LeafTurnId),
                UpdatedAt = x.UpdatedAt,
            })
            .OrderByDescending(x => x.Id), request, cancellationToken);
        return Ok(result);
    }

    internal static async Task<PagedResult<ChatsResponse>> GetChatsForGroupAsync(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption, ChatsQuery request, CancellationToken cancellationToken)
    {
        int? chatGroupId = request.GroupId != null ? idEncryption.DecryptChatGroupId(request.GroupId) : null;
        IQueryable<Chat> query = db.Chats
            .Where(x => x.UserId == currentUser.Id && !x.IsArchived && x.ChatGroupId == chatGroupId)
            .OrderByDescending(x => x.IsTopMost)
            .ThenByDescending(x => x.UpdatedAt);
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            query = query.Where(x => x.Title.Contains(request.Query) || x.ChatTags.Any(t => t.Name == request.Query));
        }

        PagedResult<ChatsResponse> result = await PagedResult.FromQuery(query
            .Select(x => new ChatsResponse()
            {
                Id = idEncryption.EncryptChatId(x.Id),
                Title = x.Title,
                IsTopMost = x.IsTopMost,
                IsShared = x.ChatShares.Count != 0,
                GroupId = idEncryption.EncryptChatGroupId(x.ChatGroupId),
                Tags = x.ChatTags.Select(x => x.Name).ToArray(),
                Spans = x.ChatSpans.Select(span => new ChatSpanDto
                {
                    SpanId = span.SpanId,
                    Enabled = span.Enabled,
                    SystemPrompt = span.ChatConfig.SystemPrompt,
                    ModelId = span.ChatConfig.ModelId,
                    ModelName = span.ChatConfig.Model.Name,
                    ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
                    Temperature = span.ChatConfig.Temperature,
                    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
                    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                    ImageSize = (DBKnownImageSize)span.ChatConfig.ImageSizeId,
                    Mcps = span.ChatConfig.ChatConfigMcps.Select(x => new ChatSpanMcp { Id = x.McpServerId, CustomHeaders = x.CustomHeaders }).ToArray()
                }).ToArray(),
                LeafTurnId = idEncryption.EncryptTurnId(x.LeafTurnId),
                UpdatedAt = x.UpdatedAt,
            }),
            request,
            cancellationToken);
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<ChatsResponse>> CreateChat([FromBody] EncryptedCreateChatRequest encryptedRequest, [FromServices] UserModelManager userModelManager, CancellationToken cancellationToken)
    {
        CreateChatRequest request = encryptedRequest.Decrypt(idEncryption);
        Dictionary<short, UserModel> validModels = await userModelManager.GetValidModelsByUserId(currentUser.Id)
            .ToDictionaryAsync(k => k.ModelId, v => v, cancellationToken);
        if (validModels.Count == 0)
        {
            return BadRequest("No model available.");
        }

        if (request.GroupId != null)
        {
            // ensure group exists
            bool groupExists = await db.ChatGroups.AnyAsync(x => x.Id == request.GroupId.Value && x.UserId == currentUser.Id, cancellationToken);
            if (!groupExists)
            {
                return BadRequest("Group not found");
            }
        }

        Chat chat = new()
        {
            UserId = currentUser.Id,
            Title = request.Title,
            ChatGroupId = request.GroupId,
            IsTopMost = false,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            UpdatedAt = DateTime.UtcNow,
        };

        Chat? lastChat = await db.Chats
            .Include(x => x.ChatSpans.OrderBy(x => x.SpanId)).ThenInclude(x => x.ChatConfig).ThenInclude(x => x.ChatConfigMcps)
            .Where(x => x.UserId == currentUser.Id && !x.IsArchived && x.ChatSpans.Any())
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastChat != null && lastChat.ChatSpans.All(cs => validModels.ContainsKey(cs.ChatConfig.ModelId)))
        {
            chat.ChatSpans = [.. lastChat.ChatSpans.Select((cs, i) =>
            {
                ChatSpan newCs = new()
                {
                    Enabled = cs.Enabled,
                    ChatConfig = cs.ChatConfig.Clone(),
                    SpanId = (byte)i,
                };
                newCs.ChatConfig.Id = 0;
                newCs.ChatConfig.Model = validModels[cs.ChatConfig.ModelId].Model;
                return newCs;
            })];
        }
        else
        {
            PromptDto defaultPrompt = await PromptsController.GetDefaultPrompt(db, currentUser.Id, cancellationToken);
            chat.ChatSpans =
            [
                new()
                {
                    SpanId = 0,
                    Enabled = true,
                    ChatConfig = new ChatConfig
                    {
                        ModelId = validModels.First().Key,
                        Model = validModels.First().Value.Model,
                        Temperature = null,
                        WebSearchEnabled = false,
                        CodeExecutionEnabled = false,
                        MaxOutputTokens = null,
                        ReasoningEffort = 0,
                        SystemPrompt = defaultPrompt.Content,
                    }
                }
            ];
        }
        db.Chats.Add(chat);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), new ChatsResponse()
        {
            Id = idEncryption.EncryptChatId(chat.Id),
            Title = chat.Title,
            IsTopMost = chat.IsTopMost,
            IsShared = false,
            GroupId = idEncryption.EncryptChatGroupId(chat.ChatGroupId),
            Tags = [],
            Spans = [.. chat.ChatSpans.Select(ChatSpanDto.FromDB)],
            LeafTurnId = idEncryption.EncryptTurnId(chat.LeafTurnId),
            UpdatedAt = chat.UpdatedAt,
        });
    }

    [HttpDelete("{encryptedChatId}")]
    public async Task<IActionResult> DeleteChats(string encryptedChatId, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        bool exists = await db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == currentUser.Id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        if (currentUser.IsAdmin)
        {
            await db.Chats
                .Where(x => x.Id == chatId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            await db.Chats
                .Where(x => x.Id == chatId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsArchived, true), cancellationToken);
        }

        return NoContent();
    }

    [HttpPut("{encryptedChatId}")]
    public async Task<IActionResult> UpdateChats(string encryptedChatId, [FromBody] UpdateChatsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        DecryptedUpdateChatsRequest req = request.Decrypt(idEncryption);

        Chat? chat = await db.Chats
            .Where(x => x.Id == idEncryption.DecryptChatId(encryptedChatId) && x.UserId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        string? error = await req.Validate(db, chat.Id, currentUser);
        if (error != null)
        {
            return BadRequest(error);
        }

        req.ApplyToChats(chat);
        if (db.ChangeTracker.HasChanges())
        {
            chat.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("{encryptedChatId}/share")]
    public async Task<ActionResult<ChatShareDto[]>> ListShared(string encryptedChatId, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatShares)
            .FirstOrDefaultAsync(x => x.Id == chatId && x.UserId == currentUser.Id, cancellationToken);

        if (chat == null)
        {
            return NotFound();
        }

        ChatShareDto[] result = [.. chat.ChatShares.Select(x => ChatShareDto.FromDB(x, idEncryption))];
        return Ok(result);
    }

    [HttpPost("{encryptedChatId}/share")]
    public async Task<ActionResult<ChatShareDto>> CreateShared(string encryptedChatId,
        DateTimeOffset validBefore,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] CurrentUser user,
        [FromServices] HostUrlService hostUrlService,
        CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        bool isChatOwner = await db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == user.Id, cancellationToken);
        if (!isChatOwner)
        {
            return Forbid();
        }

        ChatShare cs = new()
        {
            ChatId = chatId,
            ExpiresAt = validBefore,
            CreatedAt = DateTime.UtcNow,
            SnapshotTime = DateTime.UtcNow,
        };
        db.ChatShares.Add(cs);
        await db.SaveChangesAsync(cancellationToken);

        ChatShareDto dto = ChatShareDto.FromDB(cs, idEncryption);
        return Created(dto.ToUrl(hostUrlService), dto);
    }

    [HttpDelete("{encryptedChatId}/share")]
    public async Task<ActionResult<int>> DeleteAllShared(string encryptedChatId, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        bool isChatOwner = await db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == currentUser.Id, cancellationToken);
        if (!isChatOwner)
        {
            return Forbid();
        }

        int rowsEffected = await db.ChatShares
            .Where(x => x.ChatId == chatId)
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(rowsEffected);
    }
}
