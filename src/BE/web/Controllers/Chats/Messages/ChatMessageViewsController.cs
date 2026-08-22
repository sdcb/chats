using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Messages;

[Route("api/chats/{chatId}/messages"), Authorize]
public sealed class ChatMessageViewsController(
    ChatsDB db,
    CurrentUser currentUser,
    IUrlEncryptionService urlEncryption,
    ChatMessageViewService messageViews) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChatMessageViewDto>> GetInitial(string chatId, CancellationToken cancellationToken)
    {
        int decryptedChatId = urlEncryption.DecryptChatId(chatId);
        if (!await db.Chats.AnyAsync(x => x.Id == decryptedChatId && x.UserId == currentUser.Id, cancellationToken))
        {
            return NotFound();
        }

        ChatMessageViewDto result = (await messageViews.GetInitialViewAsync(decryptedChatId, null, cancellationToken))!;
        if (EtagCacheHelper.TryHandleNotModified(this, "chat-message-view", result, CreateDownloadUrlRequest.GetCurrentRefreshBucket()))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        return Ok(result);
    }

    [HttpGet("{turnId}/subtree")]
    public async Task<ActionResult<ChatMessageViewDto>> GetSubtree(string chatId, string turnId, CancellationToken cancellationToken)
    {
        int decryptedChatId = urlEncryption.DecryptChatId(chatId);
        if (!await db.Chats.AnyAsync(x => x.Id == decryptedChatId && x.UserId == currentUser.Id, cancellationToken))
        {
            return NotFound();
        }

        ChatMessageViewDto? result = await messageViews.GetSubtreeViewAsync(
            decryptedChatId,
            urlEncryption.DecryptTurnId(turnId),
            null,
            cancellationToken);
        if (result == null) return NotFound();
        if (EtagCacheHelper.TryHandleNotModified(this, "chat-message-subtree", result, CreateDownloadUrlRequest.GetCurrentRefreshBucket()))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        return Ok(result);
    }
}
