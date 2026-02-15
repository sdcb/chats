using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.BE.Services;
using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;

namespace Chats.BE.Controllers.Chats.Messages;

[Route("api/messages"), Authorize]
public class MessagesController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService urlEncryption) : ControllerBase
{
    [HttpGet("{chatId}")]
    public async Task<ActionResult<TurnDto[]>> GetTurns(string chatId, [FromServices] FileUrlProvider fup, CancellationToken cancellationToken)
    {
        TurnDto[] messages = await db.ChatTurns
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileImageInfo)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentThink)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Where(m => m.ChatId == urlEncryption.DecryptChatId(chatId) && m.Chat.UserId == currentUser.Id && m.Steps.Any())
            .Select(x => new ChatMessageTemp()
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Role = x.IsUser ? DBChatRole.User : DBChatRole.Assistant,
                Steps = x.Steps
                    .OrderBy(s => s.Id)
                    .ToArray(),
                CreatedAt = x.Steps.First().CreatedAt,
                SpanId = x.SpanId,
                Usage = x.IsUser || x.Steps.First().Usage == null ? null : new ChatMessageTempUsage()
                {
                    ModelId = x.Steps.First().Usage!.ModelId,
                    ModelName = x.Steps.First().Usage!.Model.Name,
                    ModelProviderId = x.Steps.First().Usage!.Model.ModelKey.ModelProviderId,
                },
                Reaction = x.ReactionId,
            })
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToDto(urlEncryption, fup))
            .ToArrayAsync(cancellationToken);

        if (EtagCacheHelper.TryHandleNotModified(this, "messages-turns", messages))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(messages);
    }

    [HttpGet("{chatId}/{encryptedTurnId}/generate-info")]
    public async Task<ActionResult<StepGenerateInfoDto[]>> GetTurnGenerateInfo(string chatId, string encryptedTurnId, CancellationToken cancellationToken)
    {
        long turnId = urlEncryption.DecryptTurnId(encryptedTurnId);
        int decryptedChatId = urlEncryption.DecryptChatId(chatId);

        IQueryable<ChatTurn> turns = db.ChatTurns
            .Where(x => x.Id == turnId && x.ChatId == decryptedChatId);

        if (!currentUser.IsAdmin)
        {
            turns = turns.Where(x => x.Chat.UserId == currentUser.Id);
        }

        StepGenerateInfoDto[] stepInfos = await turns
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

        if (EtagCacheHelper.TryHandleNotModified(this, "messages-turn-generate-info", stepInfos))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(stepInfos);
    }

    [HttpGet("{chatId}/step/{encryptedStepId}/generate-info")]
    public async Task<ActionResult<StepGenerateInfoDto>> GetStepGenerateInfo(string chatId, string encryptedStepId, CancellationToken cancellationToken)
    {
        long stepId = urlEncryption.DecryptStepId(encryptedStepId);
        int decryptedChatId = urlEncryption.DecryptChatId(chatId);

        IQueryable<Step> steps = db.Steps
            .Where(s => s.Id == stepId && s.Turn.ChatId == decryptedChatId && s.Usage != null);

        if (!currentUser.IsAdmin)
        {
            steps = steps.Where(s => s.Turn.Chat.UserId == currentUser.Id);
        }

        StepGenerateInfoDto? stepInfo = await steps
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

        if (EtagCacheHelper.TryHandleNotModified(this, "messages-step-generate-info", stepInfo))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(stepInfo);
    }

    [HttpPut("{encryptedTurnId}/reaction/up")]
    public async Task<ActionResult> ReactionUp(string encryptedTurnId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedTurnId, reactionId: true, cancellationToken);
    }

    [HttpPut("{encryptedTurnId}/reaction/down")]
    public async Task<ActionResult> ReactionDown(string encryptedTurnId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedTurnId, reactionId: false, cancellationToken);
    }

    [HttpPut("{encryptedTurnId}/reaction/clear")]
    public async Task<ActionResult> ReactionClear(string encryptedTurnId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedTurnId, reactionId: null, cancellationToken);
    }

    private async Task<ActionResult> ReactionPrivate(string encryptedTurnId, bool? reactionId, CancellationToken cancellationToken)
    {
        long messageId = urlEncryption.DecryptTurnId(encryptedTurnId);
        ChatTurn? message = await db.ChatTurns
            .Include(x => x.Chat)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message == null)
        {
            return NotFound();
        }

        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        message.ReactionId = reactionId;
        message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPatch("{turnId}/{contentId}/text")]
    public async Task<ActionResult<ContentResponseItem>> PatchTextInPlace(string turnId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        CancellationToken cancellationToken)
    {
        StepContent? messageContent = await db.StepContents
            .Include(x => x.Step).ThenInclude(x => x.Turn).ThenInclude(x => x.Chat)
            .Include(x => x.StepContentText)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptMessageContentId(contentId) && x.Step.TurnId == urlEncryption.DecryptTurnId(turnId), cancellationToken);
        if (messageContent == null)
        {
            return NotFound();
        }
        if (messageContent.StepContentText == null)
        {
            return BadRequest("Content is not text");
        }
        if (messageContent.Step.Turn.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        messageContent.StepContentText!.Content = content.Text;
        messageContent.Step.Turn.Chat.UpdatedAt = DateTime.UtcNow;
        messageContent.Step.Edited = true;
        await db.SaveChangesAsync(cancellationToken);

        ContentResponseItem resp = ContentResponseItem.FromContent(messageContent, fup, urlEncryption);
        return Ok(resp);
    }

    [HttpPatch("{turnId}/{contentId}/text-and-save-new")]
    public async Task<ActionResult<ResponseMessageDto>> PatchTextAndSaveNew(string turnId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        [FromServices] ClientInfoManager clientInfoManager,
        CancellationToken cancellationToken)
    {
        ChatTurn? message = await db.ChatTurns
            .Include(x => x.Chat)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentThink)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Include(x => x.Steps).ThenInclude(x => x.Usage!.Model.ModelKey)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptTurnId(turnId), cancellationToken);
        if (message == null)
        {
            return NotFound();
        }
        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }
        StepContent? textContent = message.Steps.SelectMany(x => x.StepContents).FirstOrDefault(x => x.Id == urlEncryption.DecryptMessageContentId(contentId));
        if (textContent == null)
        {
            return NotFound();
        }
        if (textContent.StepContentText == null)
        {
            return BadRequest("Content is not text");
        }

        ContentRequestItem[] newContent = [.. ContentRequestItem.FromDB([.. message.Steps.SelectMany(x => x.StepContents)], urlEncryption, textContent.Id, content)];

        StepContent[] stepContents = await StepContentExtensions.FromRequest(newContent, fup, cancellationToken);
        ClientInfo clientInfo = await clientInfoManager.GetClientInfo(cancellationToken);
        ChatTurn turn = new()
        {
            SpanId = message.SpanId,
            ChatId = message.ChatId,
            ParentId = message.ParentId,
            IsUser = message.IsUser,
            Steps = [.. message.Steps.Select(x => new Step()
            {
                StepContents = [.. stepContents],
                Edited = true, // Mark as edited since we are creating a new message
                ChatRoleId = message.IsUser ? (byte)DBChatRole.User : (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Usage = message.Steps.First().Usage != null ? new UserModelUsage()
                {
                    ModelId = message.Steps.First().Usage!.ModelId,
                    UserId = currentUser.Id,
                    FinishReasonId = (byte)DBFinishReason.Success,
                    SegmentCount = 1,
                    InputFreshTokens = message.Steps.First().Usage!.InputFreshTokens,
                    InputCachedTokens = message.Steps.First().Usage!.InputCachedTokens,
                    OutputTokens = ChatService.Tokenizer.CountTokens(content.Text),
                    ClientInfoId = clientInfo.Id, // Use FK instead of navigation property to avoid EF Core collection modification issue
                    ReasoningTokens = 0,
                    IsUsageReliable = false,
                    PreprocessDurationMs = 0,
                    FirstResponseDurationMs = 0,
                    PostprocessDurationMs = 0,
                    TotalDurationMs = 0,
                    InputFreshCost = 0,
                    OutputCost = 0,
                    InputCachedCost = 0,
                    BalanceTransactionId = null,
                    UsageTransactionId = null,
                } : null,
            })],
            ChatConfigId = message.ChatConfigId,
        };
        db.ChatTurns.Add(turn);
        message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatMessageTemp temp = ChatMessageTemp.FromDB(turn);
        return Ok(temp.ToDto(urlEncryption, fup));
    }

    [HttpDelete("{encryptedTurnId}/{contentId}")]
    public async Task<ActionResult> DeleteTurnContent(string encryptedTurnId, string contentId, CancellationToken cancellationToken)
    {
        long turnId = urlEncryption.DecryptTurnId(encryptedTurnId);
        long decryptedContentId = urlEncryption.DecryptMessageContentId(contentId);
        StepContent? messageContent = await db.StepContents
            .Include(x => x.Step.Turn.Chat)
            .FirstOrDefaultAsync(x => x.Id == decryptedContentId && x.Step.TurnId == turnId, cancellationToken);
        if (messageContent == null)
        {
            return NotFound();
        }
        if (messageContent.Step.Turn.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }
        db.StepContents.Remove(messageContent);
        messageContent.Step.Turn.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpDelete("{encryptedTurnId}")]
    public async Task<ActionResult<string[]>> DeleteTurn(string encryptedTurnId, string? encryptedLeafMessageId, CancellationToken cancellationToken)
    {
        long turnId = urlEncryption.DecryptTurnId(encryptedTurnId);
        long? leafTurnId = urlEncryption.DecryptTurnIdOrEmpty(encryptedLeafMessageId);
        ChatTurn? turn = await db.ChatTurns
            .Include(x => x.Chat.ChatTurns).ThenInclude(turn => turn.ChatDockerSessions)
            .FirstOrDefaultAsync(x => x.Id == turnId, cancellationToken);
        if (turn == null)
        {
            return NotFound();
        }
        if (turn.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        ChatTurn? leafMessage = leafTurnId == null ? null : turn.Chat.ChatTurns.FirstOrDefault(x => x.Id == leafTurnId);
        if (leafTurnId != null)
        {
            if (leafMessage == null)
            {
                return BadRequest("Leaf message not found");
            }
            else if (leafMessage.ChatId != turn.ChatId)
            {
                return BadRequest("Leaf message does not belong to the same chat");
            }
        }

        List<ChatTurn> turnsQueue = [turn];
        List<ChatTurn> toDeleteTurns = [];
        while (turnsQueue.Count > 0)
        {
            toDeleteTurns.AddRange(turnsQueue);
            turnsQueue = [.. turn.Chat.ChatTurns.Where(x => x.ParentId != null && turnsQueue.Any(toDelete => toDelete.Id == x.ParentId.Value))];
        }
        foreach (ChatTurn toDeleteTurn in toDeleteTurns)
        {
            // Deassociate docker sessions
            toDeleteTurn.ChatDockerSessions.Clear();
            turn.Chat.ChatTurns.Remove(toDeleteTurn);
        }
        turn.Chat.LeafTurnId = leafTurnId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(toDeleteTurns.Select(x => urlEncryption.EncryptTurnId(x.Id)).ToArray());
    }
}
