using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.Tokenizers;
using Chats.BE.Services;

namespace Chats.BE.Controllers.Chats.Messages;

[Route("api/messages"), Authorize]
public class MessagesController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService urlEncryption) : ControllerBase
{
    [HttpGet("{chatId}")]
    public async Task<ActionResult<MessageDto[]>> GetMessages(string chatId, [FromServices] FileUrlProvider fup, CancellationToken cancellationToken)
    {
        MessageDto[] messages = await db.ChatTurns
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileContentType)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Where(m => m.ChatId == urlEncryption.DecryptChatId(chatId) && m.Chat.UserId == currentUser.Id)
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
                    Reaction = x.ReactionId,
                },
            })
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToDto(urlEncryption, fup))
            .ToArrayAsync(cancellationToken);

        return Ok(messages);
    }

    [HttpPut("{encryptedMessageId}/reaction/up")]
    public async Task<ActionResult> ReactionUp(string encryptedMessageId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedMessageId, reactionId: true, cancellationToken);
    }

    [HttpPut("{encryptedMessageId}/reaction/down")]
    public async Task<ActionResult> ReactionDown(string encryptedMessageId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedMessageId, reactionId: false, cancellationToken);
    }

    [HttpPut("{encryptedMessageId}/reaction/clear")]
    public async Task<ActionResult> ReactionClear(string encryptedMessageId, CancellationToken cancellationToken)
    {
        return await ReactionPrivate(encryptedMessageId, reactionId: null, cancellationToken);
    }

    private async Task<ActionResult> ReactionPrivate(string encryptedMessageId, bool? reactionId, CancellationToken cancellationToken)
    {
        long messageId = urlEncryption.DecryptMessageId(encryptedMessageId);
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

    [HttpPatch("{messageId}/{contentId}/text")]
    public async Task<ActionResult<ContentResponseItem>> PatchTextInPlace(string messageId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        CancellationToken cancellationToken)
    {
        StepContent? messageContent = await db.StepContents
            .Include(x => x.Step).ThenInclude(x => x.Turn).ThenInclude(x => x.Chat)
            .Include(x => x.StepContentText)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptMessageContentId(contentId) && x.StepId == urlEncryption.DecryptMessageId(messageId), cancellationToken);
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

    [HttpPatch("{messageId}/{contentId}/text-and-save-new")]
    public async Task<ActionResult<ResponseMessageDto>> PatchTextAndSaveNew(string messageId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        [FromServices] ClientInfoManager clientInfoManager,
        CancellationToken cancellationToken)
    {
        ChatTurn? message = await db.ChatTurns
            .Include(x => x.Chat)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Include(x => x.Steps).ThenInclude(x => x.Usage!.Model.ModelKey)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptMessageId(messageId), cancellationToken);
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

        ContentRequestItem[] newContent = [.. ContentRequestItem.FromDB(message.MessageContents, urlEncryption, textContent.Id, content)];

        StepContent[] stepContents = await StepContent.FromRequest(newContent, fup, cancellationToken);
        ChatTurn newMessage = new()
        {
            SpanId = message.SpanId,
            ChatId = message.ChatId,
            ParentId = message.ParentId,
            IsUser = message.IsUser,
            Steps = [.. message.Steps.Select(x => new Step()
            {
                StepContents = stepContents,
                Edited = true, // Mark as edited since we are creating a new message
                ChatRoleId = message.IsUser ? (byte)DBChatRole.User : (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Usage = message.Steps.First().Usage != null ? new UserModelUsage()
                {
                    ModelId = message.Steps.First().Usage!.ModelId,
                    Model = message.Steps.First().Usage!.Model,
                    UserId = currentUser.Id,
                    FinishReasonId = (byte)DBFinishReason.Success,
                    SegmentCount = 1,
                    InputTokens = message.Steps.First().Usage!.InputTokens,
                    OutputTokens = ChatService.DefaultTokenizer.CountTokens(content.Text),
                    ReasoningTokens = 0,
                    IsUsageReliable = false,
                    PreprocessDurationMs = 0,
                    FirstResponseDurationMs = 0,
                    PostprocessDurationMs = 0,
                    TotalDurationMs = 0,
                    InputCost = 0,
                    OutputCost = 0,
                    BalanceTransactionId = null,
                    UsageTransactionId = null,
                } : null,
            })],
            ChatConfigId = message.ChatConfigId,
        };
        db.chatTurn.Add(newMessage);
        message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatMessageTemp temp = ChatMessageTemp.FromDB(newMessage);
        return Ok(temp.ToDto(urlEncryption, fup));
    }

    [HttpDelete("{messageId}/{contentId}")]
    public async Task<ActionResult> DeleteMessageContent(string messageId, string contentId, CancellationToken cancellationToken)
    {
        long decryptedMessageId = urlEncryption.DecryptMessageId(messageId);
        long decryptedContentId = urlEncryption.DecryptMessageContentId(contentId);
        MessageContent? messageContent = await db.MessageContents
            .Include(x => x.Message)
            .Include(x => x.Message.Chat)
            .FirstOrDefaultAsync(x => x.Id == decryptedContentId && x.MessageId == decryptedMessageId, cancellationToken);
        if (messageContent == null)
        {
            return NotFound();
        }
        if (messageContent.Message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }
        db.MessageContents.Remove(messageContent);
        messageContent.Message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpDelete("{encryptedMessageId}")]
    public async Task<ActionResult<string[]>> DeleteMessage(string encryptedMessageId, string? encryptedLeafMessageId, CancellationToken cancellationToken)
    {
        long messageId = urlEncryption.DecryptMessageId(encryptedMessageId);
        long? leafMessageId = urlEncryption.DecryptMessageIdOrNull(encryptedLeafMessageId);
        ChatTurn? message = await db.Messages
            .Include(x => x.Chat)
            .Include(x => x.Chat.Messages)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message == null)
        {
            return NotFound();
        }
        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        ChatTurn? leafMessage = leafMessageId == null ? null : message.Chat.Messages.FirstOrDefault(x => x.Id == leafMessageId);
        if (leafMessageId != null)
        {
            if (leafMessage == null)
            {
                return BadRequest("Leaf message not found");
            }
            else if (leafMessage.ChatId != message.ChatId)
            {
                return BadRequest("Leaf message does not belong to the same chat");
            }
        }

        List<ChatTurn> messagesQueue = [message];
        List<ChatTurn> toDeleteMessages = [];
        while (messagesQueue.Count > 0)
        {
            toDeleteMessages.AddRange(messagesQueue);
            messagesQueue = message.Chat.Messages
                .Where(x => x.ParentId != null && messagesQueue.Any(toDelete => toDelete.Id == x.ParentId.Value))
                .ToList();
        }
        foreach (ChatTurn toDeleteMessage in toDeleteMessages)
        {
            message.Chat.Messages.Remove(toDeleteMessage);
        }
        message.Chat.LeafMessageId = leafMessageId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(toDeleteMessages.Select(x => urlEncryption.EncryptMessageId(x.Id)).ToArray());
    }
}
