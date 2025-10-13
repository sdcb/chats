using Chats.BE.Controllers.Admin.AdminMessage.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.BE.DB.Enums;

namespace Chats.BE.Controllers.Admin.AdminMessage;

[Route("api/admin"), AuthorizeAdmin]
public class AdminMessageController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService urlEncryption) : ControllerBase
{
    [HttpGet("chats")]
    public async Task<ActionResult<PagedResult<AdminChatsDto>>> GetAdminChats([FromQuery] AdminChatsQueryRequest req, CancellationToken cancellationToken)
    {
        IQueryable<Chat> chats = db.Chats
            .Where(x => x.User.Role != "admin" || x.UserId == currentUser.Id);
        
        // 按用户名搜索
        if (!string.IsNullOrEmpty(req.User))
        {
            chats = chats.Where(x => x.User.UserName.StartsWith(req.User));
        }
        
        // 按消息内容搜索
        if (!string.IsNullOrEmpty(req.Content))
        {
            chats = chats.Where(x => x.Title.Contains(req.Content));
        }

        return await PagedResult.FromQuery(chats
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminChatsDto
            {
                Id = x.Id.ToString(),
                CreatedAt = x.CreatedAt,
                IsDeleted = x.IsArchived,
                IsShared = x.ChatShares.Any(),
                Title = x.Title,
                UserName = x.User.UserName,
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
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                    ImageSize = (DBKnownImageSize)span.ChatConfig.ImageSizeId,
                    Mcps = span.ChatConfig.ChatConfigMcps.Select(mcp => new ChatSpanMcp
                    {
                        Id = mcp.McpServerId,
                        CustomHeaders = mcp.CustomHeaders
                    }).ToArray()
                }).ToArray(),
            }), req, cancellationToken);
    }

    [HttpGet("message-details")]
    public async Task<ActionResult<ChatsResponseWithMessage>> GetAdminMessage(int chatId,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        ChatsResponseWithMessage? resp = await InternalGetChatWithMessages(db, urlEncryption, chatId, fup, cancellationToken);
        return Ok(resp);
    }

    [HttpGet("message-details/{encryptedTurnId}/generate-info")]
    public async Task<ActionResult<StepGenerateInfoDto[]>> GetAdminTurnGenerateInfo(int chatId, string encryptedTurnId, CancellationToken cancellationToken)
    {
        long turnId = urlEncryption.DecryptTurnId(encryptedTurnId);
        
        var stepInfos = await db.ChatTurns
            .Where(x => x.Id == turnId && x.ChatId == chatId)
            .SelectMany(x => x.Steps
                .Where(s => s.Usage != null)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new StepGenerateInfoDto
                {
                    InputTokens = s.Usage!.InputTokens,
                    OutputTokens = s.Usage!.OutputTokens,
                    InputPrice = s.Usage!.InputCost,
                    OutputPrice = s.Usage!.OutputCost,
                    ReasoningTokens = s.Usage!.ReasoningTokens,
                    Duration = s.Usage!.TotalDurationMs,
                    ReasoningDuration = s.Usage!.ReasoningDurationMs,
                    FirstTokenLatency = s.Usage!.FirstResponseDurationMs,
                }))
            .ToArrayAsync(cancellationToken);

        if (stepInfos.Length == 0)
        {
            return NotFound();
        }

        return Ok(stepInfos);
    }

    internal static async Task<ChatsResponseWithMessage?> InternalGetChatWithMessages(ChatsDB db, IUrlEncryptionService urlEncryption, int chatId, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        ChatsResponse? chats = await db.Chats
            .Where(x => x.Id == chatId)
            .Select(x => new ChatsResponse()
            {
                Id = urlEncryption.EncryptChatId(x.Id),
                Title = x.Title,
                IsShared = x.ChatShares.Any(),
                IsTopMost = x.IsTopMost,
                GroupId = urlEncryption.EncryptChatGroupId(x.ChatGroupId),
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
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                    ImageSize = (DBKnownImageSize)span.ChatConfig.ImageSizeId,
                    Mcps = span.ChatConfig.ChatConfigMcps.Select(mcp => new ChatSpanMcp
                    {
                        Id = mcp.McpServerId,
                        CustomHeaders = mcp.CustomHeaders
                    }).ToArray()
                }).ToArray(),
                LeafTurnId = urlEncryption.EncryptTurnId(x.LeafTurnId),
                UpdatedAt = x.UpdatedAt,
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (chats == null) return null;

        return chats.WithMessages(await db.ChatTurns
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileContentType)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Where(m => m.ChatId == chatId && m.Steps.Any())
            .Select(x => new ChatMessageTemp()
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Role = x.IsUser ? DBChatRole.User : DBChatRole.Assistant,
                Content = x.Steps
                    .SelectMany(x => x.StepContents)
                    .OrderBy(x => x.Id)
                    .ToArray(),
                CreatedAt = x.Steps.First().CreatedAt,
                SpanId = x.SpanId,
                Edited = x.Steps.Any(x => x.Edited),
                Usage = x.IsUser ? null : new ChatMessageTempUsage()
                {
                    ModelId = x.Steps.First().Usage!.ModelId,
                    ModelName = x.Steps.First().Usage!.Model.Name,
                    ModelProviderId = x.Steps.First().Usage!.Model.ModelKey.ModelProviderId,
                },
                Reaction = x.ReactionId,
            })
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToDto(urlEncryption, fup))
            .ToArrayAsync(cancellationToken));
    }
}
