using Chats.Web.Controllers.Admin.AdminMessage;
using Chats.Web.Controllers.Chats.Messages.Dtos;
using Chats.Web.Controllers.Chats.UserChats.Dtos;
using Chats.Web.DB;
using Chats.Web.Infrastructure;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Controllers.Public.SharedMessage;

[Route("api/public/chat-share")]
public class SharedChatController(ChatsDB db) : ControllerBase
{
    [HttpGet("{encryptedChatShareId}")]
    public async Task<ActionResult<ChatsResponseWithMessage>> GetSharedChat(string encryptedChatShareId,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        int chatShareId = idEncryption.DecryptChatShareId(encryptedChatShareId);
        ChatShare? chatShare = await db.ChatShares.FirstOrDefaultAsync(x => x.Id == chatShareId, cancellationToken);
        if (chatShare == null || chatShare.ExpiresAt < DateTime.UtcNow)
        {
            return NotFound();
        }

        ChatsResponseWithMessage data = (await AdminMessageController.InternalGetChatWithMessages(db, idEncryption, chatShare.ChatId, fup, cancellationToken))!;
        data.Messages = data.Messages.Where(x => x.CreatedAt <= chatShare.SnapshotTime).ToArray();
        return Ok(data);
    }

    [HttpGet("{encryptedChatShareId}/{encryptedTurnId}/generate-info")]
    public async Task<ActionResult<StepGenerateInfoDto[]>> GetSharedTurnGenerateInfo(string encryptedChatShareId, string encryptedTurnId,
        [FromServices] IUrlEncryptionService idEncryption,
        CancellationToken cancellationToken)
    {
        int chatShareId = idEncryption.DecryptChatShareId(encryptedChatShareId);
        long turnId = idEncryption.DecryptTurnId(encryptedTurnId);
        
        ChatShare? chatShare = await db.ChatShares.FirstOrDefaultAsync(x => x.Id == chatShareId, cancellationToken);
        if (chatShare == null || chatShare.ExpiresAt < DateTime.UtcNow)
        {
            return NotFound();
        }

        StepGenerateInfoDto[] stepInfos = await db.ChatTurns
            .Where(x => x.Id == turnId && x.ChatId == chatShare.ChatId && x.Steps.First().CreatedAt <= chatShare.SnapshotTime)
            .SelectMany(x => x.Steps
                .Where(s => s.Usage != null)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new StepGenerateInfoDto
                {
                    InputCachedTokens = s.Usage!.InputCachedTokens,
                    InputOverallTokens = s.Usage!.InputFreshTokens + s.Usage!.InputCachedTokens,
                    OutputTokens = s.Usage!.OutputTokens,
                    InputFreshPrice = s.Usage!.InputFreshCost,
                    InputCachedPrice = s.Usage!.InputCachedCost,
                    InputPrice = s.Usage!.InputFreshCost + s.Usage!.InputCachedCost,
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

    [HttpGet("{encryptedChatShareId}/step/{encryptedStepId}/generate-info")]
    public async Task<ActionResult<StepGenerateInfoDto>> GetSharedStepGenerateInfo(string encryptedChatShareId, string encryptedStepId,
        [FromServices] IUrlEncryptionService idEncryption,
        CancellationToken cancellationToken)
    {
        int chatShareId = idEncryption.DecryptChatShareId(encryptedChatShareId);
        long stepId = idEncryption.DecryptStepId(encryptedStepId);
        
        ChatShare? chatShare = await db.ChatShares.FirstOrDefaultAsync(x => x.Id == chatShareId, cancellationToken);
        if (chatShare == null || chatShare.ExpiresAt < DateTime.UtcNow)
        {
            return NotFound();
        }

        StepGenerateInfoDto? stepInfo = await db.Steps
            .Where(s => s.Id == stepId && s.Turn.ChatId == chatShare.ChatId && s.CreatedAt <= chatShare.SnapshotTime && s.Usage != null)
            .Select(s => new StepGenerateInfoDto
            {
                InputCachedTokens = s.Usage!.InputCachedTokens,
                InputOverallTokens = s.Usage!.InputFreshTokens + s.Usage!.InputCachedTokens,
                OutputTokens = s.Usage!.OutputTokens,
                InputFreshPrice = s.Usage!.InputFreshCost,
                InputCachedPrice = s.Usage!.InputCachedCost,
                InputPrice = s.Usage!.InputFreshCost + s.Usage!.InputCachedCost,
                OutputPrice = s.Usage!.OutputCost,
                ReasoningTokens = s.Usage!.ReasoningTokens,
                Duration = s.Usage!.TotalDurationMs,
                ReasoningDuration = s.Usage!.ReasoningDurationMs,
                FirstTokenLatency = s.Usage!.FirstResponseDurationMs,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stepInfo == null)
        {
            return NotFound();
        }

        return Ok(stepInfo);
    }

    [HttpPut("{encryptedChatShareId}"), Authorize]
    public async Task<ActionResult<ChatShareDto>> UpdateShared(string encryptedChatShareId,
        DateTimeOffset validBefore,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] CurrentUser user,
        CancellationToken cancellationToken)
    {
        int chatShareId = idEncryption.DecryptChatShareId(encryptedChatShareId);
        ChatShare? chatShare = await db.ChatShares.FirstOrDefaultAsync(x => x.Id == chatShareId, cancellationToken);
        if (chatShare == null)
        {
            return NotFound();
        }
        bool isChatOwner = await db.Chats.AnyAsync(x => x.Id == chatShare.ChatId && x.UserId == user.Id, cancellationToken);
        if (!isChatOwner)
        {
            return Forbid();
        }
        chatShare.ExpiresAt = validBefore;
        chatShare.SnapshotTime = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ChatShareDto.FromDB(chatShare, idEncryption));
    }

    [HttpDelete("{encryptedChatShareId}"), Authorize]
    public async Task<ActionResult> DeleteShared(string encryptedChatShareId,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] CurrentUser user,
        CancellationToken cancellationToken)
    {
        int chatShareId = idEncryption.DecryptChatShareId(encryptedChatShareId);
        ChatShare? chatShare = await db.ChatShares
            .Include(x => x.Chat)
            .FirstOrDefaultAsync(x => x.Id == chatShareId, cancellationToken);
        if (chatShare == null)
        {
            return NotFound();
        }
        bool isChatOwner = chatShare.Chat.UserId == user.Id;
        if (!isChatOwner)
        {
            return Forbid();
        }
        db.ChatShares.Remove(chatShare);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
