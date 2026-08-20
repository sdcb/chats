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

    [HttpPatch("{turnId}/edit")]
    public async Task<ActionResult<TurnDto>> PatchUserMessageInPlace(string turnId, [FromBody] EditUserMessageRequest request,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        ChatTurn? sourceTurn = await LoadTurnForEdit(turnId, cancellationToken);
        ActionResult? editValidation = ValidateEditTarget(sourceTurn, expectUserMessage: true);
        if (editValidation != null)
        {
            return editValidation;
        }
        ChatTurn editableTurn = sourceTurn!;

        string? validationError = await ValidateEditRequest(request, cancellationToken);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        Step step = editableTurn.Steps.OrderBy(x => x.Id).First();
        StepContent[] existingContents = [.. step.StepContents];
        TextContentRequestItem normalizedText = request.Contents.OfType<TextContentRequestItem>().Single() with
        {
            ContextTemplate = existingContents.FirstOrDefault(x => x.ContentType == DBStepContentType.Text)?.StepContentText?.ContextTemplate,
        };
        ContentRequestItem[] requestedContents = [.. request.Contents.Select(x => x is TextContentRequestItem ? normalizedText : x)];
        StepContent[] convertedContents = await ContentRequestItem.ToMessageContents(requestedContents, fup, cancellationToken);

        db.StepContents.RemoveRange(existingContents);
        step.StepContents.Clear();
        foreach (StepContent convertedContent in convertedContents)
        {
            step.StepContents.Add(convertedContent);
        }
        step.Edited = true;
        editableTurn.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        ChatMessageTemp temp = ChatMessageTemp.FromDB(editableTurn);
        return Ok(temp.ToDto(urlEncryption, fup));
    }

    [HttpPatch("{turnId}/edit-and-save-new")]
    public async Task<ActionResult<TurnDto>> PatchUserMessageAndSaveNew(string turnId, [FromBody] EditUserMessageRequest request,
        [FromServices] FileUrlProvider fup,
        [FromServices] ClientInfoManager clientInfoManager,
        CancellationToken cancellationToken)
    {
        ChatTurn? sourceTurn = await LoadTurnForEdit(turnId, cancellationToken);
        ActionResult? editValidation = ValidateEditTarget(sourceTurn, expectUserMessage: true);
        if (editValidation != null)
        {
            return editValidation;
        }
        ChatTurn editableTurn = sourceTurn!;

        string? validationError = await ValidateEditRequest(request, cancellationToken);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        StepContent[] existingContents = [.. editableTurn.Steps.SelectMany(x => x.StepContents)];
        TextContentRequestItem normalizedText = request.Contents.OfType<TextContentRequestItem>().Single() with
        {
            ContextTemplate = existingContents.FirstOrDefault(x => x.ContentType == DBStepContentType.Text)?.StepContentText?.ContextTemplate,
        };
        ContentRequestItem[] requestedContents = [.. request.Contents.Select(x => x is TextContentRequestItem ? normalizedText : x)];
        StepContent[] convertedContents = await ContentRequestItem.ToMessageContents(requestedContents, fup, cancellationToken);
        int clientInfoId = await clientInfoManager.GetClientInfoId(cancellationToken);
        ChatTurn editedTurn = BuildEditedTurn(editableTurn, convertedContents, true, normalizedText.Text, clientInfoId);
        db.ChatTurns.Add(editedTurn);
        editableTurn.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatMessageTemp temp = ChatMessageTemp.FromDB(editedTurn);
        return Ok(temp.ToDto(urlEncryption, fup));
    }

    private async Task<ChatTurn?> LoadTurnForEdit(string turnId, CancellationToken cancellationToken)
    {
        return await db.ChatTurns
            .Include(x => x.Chat)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentThink)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .Include(x => x.Steps).ThenInclude(x => x.Usage!).ThenInclude(x => x.ModelSnapshot).ThenInclude(x => x.ModelKeySnapshot)
            .FirstOrDefaultAsync(x => x.Id == urlEncryption.DecryptTurnId(turnId), cancellationToken);
    }

    private ActionResult? ValidateEditTarget(ChatTurn? message, bool expectUserMessage)
    {
        if (message == null)
        {
            return NotFound();
        }
        if (message.Chat.UserId != currentUser.Id)
        {
            return Forbid();
        }
        if (message.IsUser != expectUserMessage)
        {
            return BadRequest(expectUserMessage
                ? "Only user messages can be edited with this endpoint"
                : "Only response messages can be edited with this endpoint");
        }
        return null;
    }

    private UserModelUsage? BuildEditedUsage(ChatTurn sourceTurn, string editedText, int clientInfoId)
    {
        UserModelUsage? sourceUsage = sourceTurn.Steps.FirstOrDefault()?.Usage;
        if (sourceUsage == null)
        {
            return null;
        }

        return new UserModelUsage
        {
            ModelSnapshotId = sourceUsage.ModelSnapshotId,
            ModelSnapshot = sourceUsage.ModelSnapshot,
            UserId = currentUser.Id,
            FinishReasonId = (byte)DBFinishReason.Success,
            SegmentCount = 1,
            InputFreshTokens = sourceUsage.InputFreshTokens,
            InputCachedTokens = sourceUsage.InputCachedTokens,
            OutputTokens = ChatService.Tokenizer.CountTokens(editedText),
            ClientInfoId = clientInfoId,
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
            SourceId = sourceUsage.SourceId,
        };
    }

    private ChatTurn BuildEditedTurn(ChatTurn sourceTurn, StepContent[] editedContents, bool isUserMessage, string editedText, int clientInfoId)
    {
        IEnumerable<Step> sourceSteps = isUserMessage ? [new Step()] : sourceTurn.Steps;
        return new ChatTurn
        {
            SpanId = sourceTurn.SpanId,
            ChatId = sourceTurn.ChatId,
            ParentId = sourceTurn.ParentId,
            IsUser = isUserMessage,
            Steps = [.. sourceSteps.Select(_ => new Step
            {
                StepContents = [.. editedContents],
                Edited = true,
                ChatRoleId = isUserMessage ? (byte)DBChatRole.User : (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Usage = BuildEditedUsage(sourceTurn, editedText, clientInfoId),
            })],
            ChatConfigSnapshotId = sourceTurn.ChatConfigSnapshotId,
        };
    }

    private async Task<string?> ValidateEditRequest(EditUserMessageRequest request, CancellationToken cancellationToken)
    {
        if (request.Contents == null || request.Contents.Length == 0)
        {
            return "Message contents are required";
        }
        TextContentRequestItem[] textContents = [.. request.Contents.OfType<TextContentRequestItem>()];
        FileContentRequestItem[] fileContents = [.. request.Contents.OfType<FileContentRequestItem>()];
        if (textContents.Length != 1 || string.IsNullOrWhiteSpace(textContents[0].Text))
        {
            return "Exactly one non-empty text content is required";
        }
        if (request.Contents.Length != textContents.Length + fileContents.Length)
        {
            return "Unsupported message content type";
        }
        if (fileContents.Length > 5)
        {
            return "Too many attachments";
        }

        int[] fileIds;
        try
        {
            fileIds = [.. fileContents.Select(x => urlEncryption.DecryptFileId(x.FileId))];
        }
        catch (Exception)
        {
            return "Invalid file ID";
        }
        if (fileIds.Distinct().Count() != fileIds.Length)
        {
            return "Duplicate file ID";
        }
        if (fileIds.Length == 0)
        {
            return null;
        }

        var files = await db.Files.Where(x => fileIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (files.Count != fileIds.Length || files.Any(x => x.CreateUserId != currentUser.Id && !currentUser.IsAdmin))
        {
            return "File not found or not accessible";
        }
        if (files.Any(x => !x.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            return "Only image attachments are supported";
        }
        return null;
    }

    [HttpPatch("{turnId}/{contentId}/text")]
    public async Task<ActionResult<ContentResponseItem>> PatchResponseTextInPlace(string turnId, string contentId, [FromBody] TextContentRequestItem content,
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
        ActionResult? editValidation = ValidateEditTarget(messageContent.Step.Turn, expectUserMessage: false);
        if (editValidation != null)
        {
            return editValidation;
        }

        messageContent.StepContentText!.Content = content.Text;
        messageContent.Step.Turn.Chat.UpdatedAt = DateTime.UtcNow;
        messageContent.Step.Edited = true;
        await db.SaveChangesAsync(cancellationToken);

        ContentResponseItem resp = ContentResponseItem.FromContent(messageContent, fup, urlEncryption);
        return Ok(resp);
    }

    [HttpPatch("{turnId}/{contentId}/text-and-save-new")]
    public async Task<ActionResult<ResponseMessageDto>> PatchResponseTextAndSaveNew(string turnId, string contentId, [FromBody] TextContentRequestItem content,
        [FromServices] FileUrlProvider fup,
        [FromServices] IUrlEncryptionService urlEncryption,
        [FromServices] ClientInfoManager clientInfoManager,
        CancellationToken cancellationToken)
    {
        ChatTurn? sourceTurn = await LoadTurnForEdit(turnId, cancellationToken);
        ActionResult? editValidation = ValidateEditTarget(sourceTurn, expectUserMessage: false);
        if (editValidation != null)
        {
            return editValidation;
        }
        ChatTurn sourceResponse = sourceTurn!;
        StepContent? targetTextContent = sourceResponse.Steps.SelectMany(x => x.StepContents).FirstOrDefault(x => x.Id == urlEncryption.DecryptMessageContentId(contentId));
        if (targetTextContent == null)
        {
            return NotFound();
        }
        if (targetTextContent.StepContentText == null)
        {
            return BadRequest("Content is not text");
        }

        ContentRequestItem[] patchedContents = [.. ContentRequestItem.FromDB([.. sourceResponse.Steps.SelectMany(x => x.StepContents)], urlEncryption, targetTextContent.Id, content)];

        StepContent[] convertedContents = await StepContentExtensions.FromRequest(patchedContents, fup, cancellationToken);
        int clientInfoId = await clientInfoManager.GetClientInfoId(cancellationToken);
        ChatTurn editedTurn = BuildEditedTurn(sourceResponse, convertedContents, false, content.Text, clientInfoId);
        db.ChatTurns.Add(editedTurn);
        sourceResponse.Chat.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ChatMessageTemp temp = ChatMessageTemp.FromDB(editedTurn);
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
