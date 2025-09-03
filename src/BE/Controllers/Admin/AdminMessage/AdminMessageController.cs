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
    public async Task<ActionResult<PagedResult<AdminChatsDto>>> GetAdminChats([FromQuery] QueryPagingRequest req, CancellationToken cancellationToken)
    {
        IQueryable<Chat> chats = db.Chats
            .Where(x => x.User.Role != "admin" || x.UserId == currentUser.Id);
        if (!string.IsNullOrEmpty(req.Query))
        {
            chats = chats.Where(x => x.User.UserName == req.Query);
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
            .Where(m => m.ChatId == chatId)
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
                    InputTokens = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.InputTokens),
                    OutputTokens = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.OutputTokens),
                    InputPrice = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.InputCost),
                    OutputPrice = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.OutputCost),
                    ReasoningTokens = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.ReasoningTokens),
                    Duration = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.TotalDurationMs),
                    ReasoningDuration = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.ReasoningDurationMs),
                    FirstTokenLatency = x.Steps.Where(x => x.Usage != null).Sum(x => x.Usage!.FirstResponseDurationMs),
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
