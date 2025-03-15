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

namespace Chats.BE.Controllers.Admin.AdminMessage;

[Route("api/admin"), AuthorizeAdmin]
public class AdminMessageController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService urlEncryption) : ControllerBase
{
    [HttpGet("chats")]
    public async Task<ActionResult<PagedResult<AdminChatsDto>>> GetAdminChats([FromQuery] PagingRequest req, CancellationToken cancellationToken)
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
                    ModelId = span.ChatConfig.ModelId,
                    ModelName = span.ChatConfig.Model.Name,
                    ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
                    Temperature = span.ChatConfig.Temperature,
                    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
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
                    ModelId = span.ChatConfig.ModelId,
                    ModelName = span.ChatConfig.Model.Name,
                    ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
                    Temperature = span.ChatConfig.Temperature,
                    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
                    MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
                    ReasoningEffort = span.ChatConfig.ReasoningEffort,
                }).ToArray(),
                LeafMessageId = urlEncryption.EncryptMessageId(x.LeafMessageId),
                UpdatedAt = x.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (chats == null) return null;

        return chats.WithMessages(await db.Messages
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentBlob)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentText)
            .Where(m => m.ChatId == chatId)
            .Select(x => new ChatMessageTemp()
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Role = (DBChatRole)x.ChatRoleId,
                Content = x.MessageContents
                    .OrderBy(x => x.Id)
                    .ToArray(),
                CreatedAt = x.CreatedAt,
                SpanId = x.SpanId,
                Edited = x.Edited,
                Usage = x.MessageResponse!.Usage == null ? null : new ChatMessageTempUsage()
                {
                    InputTokens = x.MessageResponse.Usage.InputTokens,
                    OutputTokens = x.MessageResponse.Usage.OutputTokens,
                    InputPrice = x.MessageResponse.Usage.InputCost,
                    OutputPrice = x.MessageResponse.Usage.OutputCost,
                    ReasoningTokens = x.MessageResponse.Usage.ReasoningTokens,
                    Duration = x.MessageResponse.Usage.TotalDurationMs - x.MessageResponse.Usage.PreprocessDurationMs,
                    ReasoningDuration = x.MessageResponse.Usage.ReasoningDurationMs,
                    FirstTokenLatency = x.MessageResponse.Usage.FirstResponseDurationMs,
                    ModelId = x.MessageResponse.Usage.UserModel.ModelId,
                    ModelName = x.MessageResponse.Usage.UserModel.Model.Name,
                    ModelProviderId = x.MessageResponse.Usage.UserModel.Model.ModelKey.ModelProviderId,
                    Reaction = x.MessageResponse.ReactionId,
                },
            })
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToDto(urlEncryption, fup))
            .ToArrayAsync(cancellationToken));
    }
}
