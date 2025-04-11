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
        MessageDto[] messages = await db.Messages
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentBlob)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentText)
            .Where(m => m.ChatId == urlEncryption.DecryptChatId(chatId) && m.Chat.UserId == currentUser.Id)
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
        Message? message = await db.Messages
            .Include(x => x.Chat)
            .Include(x => x.MessageResponse)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message == null)
        {
            return NotFound();
        }

        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        if (message.MessageResponse == null)
        {
            return BadRequest("Message has no response");
        }

        message.MessageResponse!.ReactionId = reactionId;
        message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("{encryptedMessageId}/edit-in-place"), Obsolete("Use PATCH {messageId}/{contentId}/text")]
    public async Task<ActionResult> EditMessageInPlace(string encryptedMessageId, [FromBody] ContentRequestItem[] content,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        long messageId = urlEncryption.DecryptMessageId(encryptedMessageId);
        Message? message = await db.Messages
            .Include(x => x.MessageContents)
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

        message.MessageContents.Clear();
        foreach (MessageContent c in await MessageContent.FromRequest(content, fup, cancellationToken))
        {
            message.MessageContents.Add(c);
        }
        message.Chat.UpdatedAt = DateTime.UtcNow;
        message.Edited = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("{encryptedMessageId}/edit-and-save-new"), Obsolete("Use PATCH {messageId}/{contentId}/text-and-save-new")]
    public async Task<ActionResult<RequestMessageDto>> EditAndSaveNew(string encryptedMessageId, [FromBody] ContentRequestItem[] content,
        [FromServices] FileUrlProvider fup,
        [FromServices] ClientInfoManager clientInfoManager,
        CancellationToken cancellationToken)
    {
        long messageId = urlEncryption.DecryptMessageId(encryptedMessageId);
        Message? message = await db.Messages
            .Include(x => x.Chat)
            .Include(x => x.MessageResponse!.Usage)
            .Include(x => x.MessageResponse!.Usage.UserModel)
            .Include(x => x.MessageResponse!.Usage.UserModel.Model)
            .Include(x => x.MessageResponse!.Usage.UserModel.Model.ModelKey)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message == null)
        {
            return NotFound();
        }
        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        Message newMessage = new()
        {
            Edited = true,
            CreatedAt = DateTime.UtcNow,
            SpanId = message.SpanId,
            ChatId = message.ChatId,
            ParentId = message.ParentId,
            ChatRoleId = message.ChatRoleId,
            ChatRole = message.ChatRole,
            MessageContents = await MessageContent.FromRequest(content, fup, cancellationToken),
        };
        if (message.MessageResponse != null)
        {
            string textPart = content.OfType<TextContentRequestItem>().FirstOrDefault()?.Text ?? "";
            newMessage.MessageResponse = new MessageResponse()
            {
                Usage = new UserModelUsage()
                {
                    UserModelId = message.MessageResponse.Usage.UserModelId,
                    UserModel = message.MessageResponse.Usage.UserModel,
                    FinishReasonId = (byte)DBFinishReason.Success,
                    SegmentCount = 1,
                    InputTokens = message.MessageResponse.Usage.InputTokens,
                    OutputTokens = ChatService.DefaultTokenizer.CountTokens(textPart),
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
                    ClientInfo = await clientInfoManager.GetClientInfo(cancellationToken),
                    CreatedAt = DateTime.UtcNow,
                },
                ChatConfigId = message.MessageResponse.ChatConfigId,
            };
        }
        db.Messages.Add(newMessage);
        message.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatMessageTemp temp = ChatMessageTemp.FromDB(newMessage);
        return Ok(temp.ToDto(urlEncryption, fup));
    }

    [HttpPatch("{messageId}/{contentId}/text")]
    public async Task<ActionResult<ContentResponseItem>> PatchTextInPlace(string messageId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        CancellationToken cancellationToken)
    {
        MessageContent? messageContent = await db.MessageContents
            .Include(x => x.Message).ThenInclude(x => x.Chat)
            .Include(x => x.MessageContentText)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptMessageContentId(contentId) && x.MessageId == urlEncryption.DecryptMessageId(messageId), cancellationToken);
        if (messageContent == null)
        {
            return NotFound();
        }
        if (messageContent.MessageContentText == null)
        {
            return BadRequest("Content is not text");
        }
        if (messageContent.Message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }

        messageContent.MessageContentText!.Content = content.Text;
        messageContent.Message.Chat.UpdatedAt = DateTime.UtcNow;
        messageContent.Message.Edited = true;
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
        Message? message = await db.Messages
            .Include(x => x.Chat)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentText)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentBlob)
            .Include(x => x.MessageContents).ThenInclude(x => x.MessageContentFile)
            .Include(x => x.MessageResponse!.Usage)
            .Include(x => x.MessageResponse!.Usage.UserModel)
            .Include(x => x.MessageResponse!.Usage.UserModel.Model)
            .Include(x => x.MessageResponse!.Usage.UserModel.Model.ModelKey)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptMessageId(messageId), cancellationToken);
        if (message == null)
        {
            return NotFound();
        }
        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }
        MessageContent? textContent = message.MessageContents.FirstOrDefault(x => x.Id == urlEncryption.DecryptMessageContentId(contentId));
        if (textContent == null)
        {
            return NotFound();
        }
        if (textContent.MessageContentText == null)
        {
            return BadRequest("Content is not text");
        }

        ContentRequestItem[] newContent = [.. ContentRequestItem.FromDB(message.MessageContents, urlEncryption, textContent.Id, content)];

        Message newMessage = new()
        {
            Edited = true,
            CreatedAt = DateTime.UtcNow,
            SpanId = message.SpanId,
            ChatId = message.ChatId,
            ParentId = message.ParentId,
            ChatRoleId = message.ChatRoleId,
            ChatRole = message.ChatRole,
            MessageContents = await MessageContent.FromRequest(newContent, fup, cancellationToken),
        };
        if (message.MessageResponse != null)
        {
            newMessage.MessageResponse = new MessageResponse()
            {
                Usage = new UserModelUsage()
                {
                    UserModelId = message.MessageResponse.Usage.UserModelId,
                    UserModel = message.MessageResponse.Usage.UserModel,
                    FinishReasonId = (byte)DBFinishReason.Success,
                    SegmentCount = 1,
                    InputTokens = message.MessageResponse.Usage.InputTokens,
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
                    ClientInfo = await clientInfoManager.GetClientInfo(cancellationToken),
                    CreatedAt = DateTime.UtcNow,
                },
                ChatConfigId = message.MessageResponse.ChatConfigId,
            };
        }
        db.Messages.Add(newMessage);
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
        Message? message = await db.Messages
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

        Message? leafMessage = leafMessageId == null ? null : message.Chat.Messages.FirstOrDefault(x => x.Id == leafMessageId);
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

        List<Message> messagesQueue = [message];
        List<Message> toDeleteMessages = [];
        while (messagesQueue.Count > 0)
        {
            toDeleteMessages.AddRange(messagesQueue);
            messagesQueue = message.Chat.Messages
                .Where(x => x.ParentId != null && messagesQueue.Any(toDelete => toDelete.Id == x.ParentId.Value))
                .ToList();
        }
        foreach (Message toDeleteMessage in toDeleteMessages)
        {
            message.Chat.Messages.Remove(toDeleteMessage);
        }
        message.Chat.LeafMessageId = leafMessageId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(toDeleteMessages.Select(x => urlEncryption.EncryptMessageId(x.Id)).ToArray());
    }
}
