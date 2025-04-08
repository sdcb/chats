using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.ChatServices.Test;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.Models.ChatServices;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chats"), Authorize]
public class ChatController(ChatStopService stopService) : ControllerBase
{
    [HttpPost("regenerate-assistant-message")]
    public async Task<IActionResult> RegenerateMessage(
        [FromBody] RegenerateAssistantMessageRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, clientInfoManager, fup, chatConfigService, dBFileService,
            cancellationToken);
    }

    [HttpPost("general")]
    public async Task<IActionResult> GeneralChat2(
        [FromBody] GeneralChatRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!req.UserMessage.OfType<TextContentRequestItem>().Any())
        {
            return BadRequest("User message must have at least one text content");
        }

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, clientInfoManager, fup, chatConfigService, dBFileService,
            cancellationToken);
    }

    private async Task<IActionResult> ChatPrivate(
        DecryptedChatRequest req,
        ChatsDB db,
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        IUrlEncryptionService idEncryption,
        BalanceService balanceService,
        ChatFactory chatFactory,
        UserModelManager userModelManager,
        ClientInfoManager clientInfoManager,
        FileUrlProvider fup,
        ChatConfigService chatConfigService,
        DBFileService dbFileService, 
        CancellationToken cancellationToken)
    {
        long firstTick = Stopwatch.GetTimestamp();

        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans).ThenInclude(x => x.ChatConfig)
            .Include(x => x.Messages.Where(x => x.ChatRoleId == (byte)DBChatRole.User || x.ChatRoleId == (byte)DBChatRole.Assistant))
            .FirstOrDefaultAsync(x => x.Id == req.ChatId && x.UserId == currentUser.Id, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        Dictionary<long, MessageLiteDtoNoContent> existingMessages = chat.Messages
            .Select(x => new MessageLiteDtoNoContent()
            {
                Id = x.Id,
                Role = (DBChatRole)x.ChatRoleId,
                ParentId = x.ParentId,
                SpanId = x.SpanId,
            })
            .ToDictionary(x => x.Id, x => x);
        bool isEmptyChat = existingMessages.Count == 0;

        // ensure chat.ChatSpan contains all span ids that in request, otherwise return error
        ChatSpan[] toGenerateSpans = null!;
        if (req is DecryptedRegenerateAssistantMessageRequest rr)
        {
            ChatSpan? span = chat.ChatSpans.FirstOrDefault(y => y.SpanId == rr.SpanId);
            if (span == null)
            {
                return BadRequest($"Invalid span id: {rr.SpanId}");
            }

            ChatSpan newSpan = span.Clone();
            newSpan.ChatConfig.ModelId = rr.ModelId;
            toGenerateSpans = [newSpan];
        }
        else if (req is DecryptedGeneralChatRequest)
        {
            toGenerateSpans = [.. chat.ChatSpans.Where(x => x.Enabled)];
        }
        if (toGenerateSpans.Length == 0)
        {
            return BadRequest("No enabled spans");
        }

        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, [.. toGenerateSpans.Select(x => x.ChatConfig.ModelId)], cancellationToken);
        {
            // ensure userModels contains all models that in toGenerateSpans
            HashSet<short> requestedModels = [.. toGenerateSpans.Select(x => x.ChatConfig.ModelId)];
            HashSet<short> existingModels = [.. userModels.Keys];
            if (!requestedModels.SetEquals(existingModels))
            {
                return BadRequest("Invalid model permission");
            }
        }

        Message? newDbUserMessage = null;
        if (req is DecryptedGeneralChatRequest generalRequest)
        {
            if (generalRequest.ParentAssistantMessageId != null)
            {
                if (!existingMessages.TryGetValue(generalRequest.ParentAssistantMessageId.Value, out MessageLiteDtoNoContent? parentMessage))
                {
                    return BadRequest("Invalid message id");
                }

                if (parentMessage.Role != DBChatRole.Assistant)
                {
                    return BadRequest("Parent message is not assistant message");
                }
            }

            newDbUserMessage = new()
            {
                ChatRoleId = (byte)DBChatRole.User,
                MessageContents = await MessageContent.FromRequest(generalRequest.UserMessage, fup, cancellationToken),
                CreatedAt = DateTime.UtcNow,
                ParentId = generalRequest.ParentAssistantMessageId,
            };
            chat.Messages.Add(newDbUserMessage);
        }
        else if (req is DecryptedRegenerateAssistantMessageRequest regenerateRequest)
        {
            if (!existingMessages.TryGetValue(regenerateRequest.ParentUserMessageId, out MessageLiteDtoNoContent? parentMessage))
            {
                return BadRequest("Invalid message id");
            }

            if (parentMessage.Role != DBChatRole.User)
            {
                return BadRequest("ParentUserMessageId is not user message");
            }
        }

        LinkedList<MessageLiteDtoNoContent> messageTreeNoContent = GetMessageTree(existingMessages, req.LastMessageId);
        MessageLiteDto[] messageTree = await FillContents(messageTreeNoContent, db, cancellationToken);

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Connection = "keep-alive";
        string stopId = stopService.CreateAndCombineCancellationToken(ref cancellationToken);
        await YieldResponse(SseResponseLine.StopId(stopId));

        UserBalance userBalance = await db.UserBalances.Where(x => x.UserId == currentUser.Id).SingleAsync(cancellationToken);
        Task<ClientInfo> clientInfoTask = clientInfoManager.GetClientInfo(cancellationToken);
        Task<FileService?> fsTask = clientInfoTask.ContinueWith(x => FileService.GetDefault(db, cancellationToken)).Unwrap();

        Channel<SseResponseLine>[] channels = [.. toGenerateSpans.Select(x => Channel.CreateUnbounded<SseResponseLine>())];
        Dictionary<ImageChatSegment, DB.File> imageFileCache = [];
        Task<ChatSpanResponse>[] streamTasks = [.. toGenerateSpans
            .Select((span, index) => ProcessChatSpan(
                currentUser,
                logger,
                chatFactory,
                fup,
                span,
                firstTick,
                req,
                chat,
                userModels[span.ChatConfig.ModelId],
                messageTree,
                newDbUserMessage,
                userBalance,
                clientInfoTask,
                imageFileCache,
                channels[index].Writer,
                cancellationToken))];

        if (isEmptyChat && req is DecryptedGeneralChatRequest generalChatRequest)
        {
            string text = generalChatRequest.UserMessage
                .OfType<TextContentRequestItem>()
                .Single()
                .Text;
            chat.Title = text[..Math.Min(50, text.Length)];
        }

        bool dbUserMessageYield = false;
        await foreach (SseResponseLine line in MergeChannels(channels).Reader.ReadAllAsync(CancellationToken.None))
        {
            if (line.Kind == SseResponseKind.End)
            {
                Message dbAssistantMessage = (Message)line.Result;
                ChatSpan chatSpan = toGenerateSpans.Single(x => x.SpanId == dbAssistantMessage.SpanId);
                dbAssistantMessage.MessageResponse!.ChatConfig = await chatConfigService.GetOrCreateChatConfig(chatSpan.ChatConfig, default);
                chat.Messages.Add(dbAssistantMessage);
                bool isLast = line.SpanId == toGenerateSpans.Last().SpanId;
                if (isLast)
                {
                    chat.LeafMessage = dbAssistantMessage;
                }
                chat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);

                if (newDbUserMessage != null && !dbUserMessageYield)
                {
                    await YieldResponse(SseResponseLine.UserMessage(newDbUserMessage, idEncryption, fup));
                    dbUserMessageYield = true;
                }
                await YieldResponse(SseResponseLine.ResponseMessage(line.SpanId!.Value, dbAssistantMessage, idEncryption, fup));
                if (isLast)
                {
                    await YieldResponse(SseResponseLine.ChatLeafMessageId(chat.LeafMessageId!.Value, idEncryption));
                }
            }
            else if (line.Kind == SseResponseKind.ImageGenerated)
            {
                ImageChatSegment image = (ImageChatSegment)line.Result;
                FileService fs = await fsTask ?? throw new InvalidOperationException("Default file service config not found.");
                DB.File file = await dbFileService.StoreImage(image, await clientInfoTask, fs, cancellationToken: default);
                imageFileCache[image] = file;
                await YieldResponse(SseResponseLine.ImageGenerated(line.SpanId!.Value, fup.CreateFileDto(file)));
            }
            else
            {
                await YieldResponse(line);
            }
        }
        cancellationToken = CancellationToken.None;
        stopService.Remove(stopId);

        // not cancellable from here
        ChatSpanResponse[] resps = await Task.WhenAll(streamTasks);

        // finish costs
        if (resps.Any(x => x.Cost.CostBalance > 0))
        {
            await balanceService.UpdateBalance(db, currentUser.Id, cancellationToken);
        }
        if (resps.Any(x => x.Cost.CostUsage))
        {
            foreach (UserModel um in userModels.Values)
            {
                await balanceService.UpdateUsage(db, um.Id, cancellationToken);
            }
        }

        // yield title
        if (isEmptyChat) await YieldTitle(chat.Title);
        return new EmptyResult();
    }

    private static async Task<MessageLiteDto[]> FillContents(LinkedList<MessageLiteDtoNoContent> noContent, ChatsDB db, CancellationToken cancellationToken)
    {
        HashSet<long> messageIds = [.. noContent.Select(x => x.Id)];
        Dictionary<long, MessageContent[]> contents = await db.MessageContents
            .Where(x => messageIds.Contains(x.MessageId))
            .Include(x => x.MessageContentBlob)
            .Include(x => x.MessageContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.MessageContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileImageInfo)
            .Include(x => x.MessageContentText)
            .GroupBy(x => x.MessageId)
            .ToDictionaryAsync(k => k.Key, v => v.ToArray(), cancellationToken);
        return [.. noContent.Select(x => x.WithContent(contents[x.Id]))];
    }

    private static async Task<ChatSpanResponse> ProcessChatSpan(
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        ChatFactory chatFactory,
        FileUrlProvider fup,
        ChatSpan chatSpan,
        long firstTick,
        DecryptedChatRequest req,
        Chat chat,
        UserModel userModel,
        IEnumerable<MessageLiteDto> messageTree,
        Message? dbUserMessage,
        UserBalance userBalance,
        Task<ClientInfo> clientInfoTask,
        Dictionary<ImageChatSegment, DB.File> imageFileCache,
        ChannelWriter<SseResponseLine> writer,
        CancellationToken cancellationToken)
    {
        List<OpenAIChatMessage> messageToSend = await ((IEnumerable<MessageLiteDto>)
        [
            ..messageTree,
            ..dbUserMessage != null ? [MessageLiteDto.FromDB(dbUserMessage)] : Array.Empty<MessageLiteDto>(),
        ])
        .ToAsyncEnumerable()
        .SelectAwait(async x => await x.ToOpenAI(fup, cancellationToken))
        .ToListAsync(cancellationToken);
        if (!string.IsNullOrEmpty(chatSpan.ChatConfig.SystemPrompt))
        {
            messageToSend.Insert(0, OpenAIChatMessage.CreateSystemMessage(chatSpan.ChatConfig.SystemPrompt));
        }

        ChatCompletionOptions cco = chatSpan.ToChatCompletionOptions(currentUser.Id, chatSpan, userModel);
        ChatExtraDetails ced = new()
        {
            TimezoneOffset = req.TimezoneOffset,
            WebSearchEnabled = chatSpan.ChatConfig.WebSearchEnabled,
        };

        InChatContext icc = new(firstTick);

        string? errorText = null;
        try
        {
            using ChatService s = chatFactory.CreateChatService(userModel.Model);

            bool responseStated = false, reasoningStarted = false;
            await foreach (InternalChatSegment seg in icc.Run(userBalance.Balance, userModel, s.ChatStreamedFEProcessed(messageToSend, cco, ced, cancellationToken)))
            {
                foreach (ChatSegmentItem item in seg.Items)
                {
                    if (item is ThinkChatSegment thinkSeg)
                    {
                        if (!reasoningStarted)
                        {
                            await writer.WriteAsync(SseResponseLine.StartReasoning(chatSpan.SpanId), cancellationToken);
                            reasoningStarted = true;
                        }
                        await writer.WriteAsync(SseResponseLine.ReasoningSegment(chatSpan.SpanId, thinkSeg.Think), cancellationToken);
                    }
                    else if (item is TextChatSegment textSeg)
                    {
                        if (!responseStated)
                        {
                            await writer.WriteAsync(SseResponseLine.StartResponse(chatSpan.SpanId, icc.ReasoningDurationMs), cancellationToken);
                            responseStated = true;
                        }
                        await writer.WriteAsync(SseResponseLine.Segment(chatSpan.SpanId, textSeg.Text), cancellationToken);
                    }
                    else if (item is ImageChatSegment imgSeg)
                    {
                        await writer.WriteAsync(SseResponseLine.ImageGeneratedTemp(chatSpan.SpanId, imgSeg), cancellationToken);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
            }
        }
        catch (ChatServiceException cse)
        {
            icc.FinishReason = cse.ErrorCode;
            errorText = cse.Message;
        }
        catch (ClientResultException e)
        {
            icc.FinishReason = DBFinishReason.UpstreamError;
            errorText = e.Message;
            logger.LogError(e, "Upstream error: {userMessageId}", req.LastMessageId);
        }
        catch (AggregateException e) when (e.InnerException is TaskCanceledException)
        {
            // do nothing if cancelled
            icc.FinishReason = DBFinishReason.Cancelled;
            errorText = e.InnerException.ToString();
        }
        catch (TaskCanceledException)
        {
            // do nothing if cancelled
            icc.FinishReason = DBFinishReason.Cancelled;
            errorText = "Conversation cancelled";
        }
        catch (UriFormatException e)
        {
            icc.FinishReason = DBFinishReason.InternalConfigIssue;
            errorText = e.Message;
            logger.LogError(e, "Invalid URL in conversation for message: {userMessageId}", req.LastMessageId);
        }
        catch (JsonException e)
        {
            icc.FinishReason = DBFinishReason.InternalConfigIssue;
            errorText = e.Message;
            logger.LogError(e, "Invalid JSON config in conversation for message: {userMessageId}", req.LastMessageId);
        }
        catch (Exception e)
        {
            icc.FinishReason = DBFinishReason.UnknownError;
            errorText = "Unknown Error";
            logger.LogError(e, "Error in conversation for message: {userMessageId}", req.LastMessageId);
        }
        finally
        {
            // cancel the conversation because following code is credit deduction related
            cancellationToken = CancellationToken.None;
        }

        // success
        // insert new assistant message
        Message dbAssistantMessage = new()
        {
            ChatId = chat.Id,
            ChatRoleId = (byte)DBChatRole.Assistant,
            SpanId = chatSpan.SpanId,
            CreatedAt = DateTime.UtcNow,
        };
        if (req is DecryptedGeneralChatRequest && dbUserMessage != null)
        {
            dbAssistantMessage.Parent = dbUserMessage;
        }
        else if (req is DecryptedRegenerateAssistantMessageRequest decryptedRegenerateAssistantMessageRequest)
        {
            dbAssistantMessage.ParentId = decryptedRegenerateAssistantMessageRequest.ParentUserMessageId;
        }
        foreach (MessageContent mc in MessageContent.FromFullResponse(icc.FullResponse, errorText, imageFileCache))
        {
            dbAssistantMessage.MessageContents.Add(mc);
        }

        if (errorText != null)
        {
            await writer.WriteAsync(SseResponseLine.Error(chatSpan.SpanId, errorText), cancellationToken);
        }
        ClientInfo clientInfo = await clientInfoTask;
        UserModelUsage usage = icc.ToUserModelUsage(currentUser.Id, userModel, clientInfo, isApi: false);
        dbAssistantMessage.MessageResponse = new MessageResponse()
        {
            Usage = usage,
        };
        await writer.WriteAsync(new SseResponseLine { Kind = SseResponseKind.End, Result = dbAssistantMessage, SpanId = chatSpan.SpanId }, cancellationToken);
        writer.Complete();
        return new ChatSpanResponse()
        {
            AssistantMessage = dbAssistantMessage,
            Cost = icc.Cost,
            SpanId = chatSpan.SpanId,
        };
    }

    static Channel<T> MergeChannels<T>(params Channel<T>[] channels)
    {
        Channel<T> outputChannel = Channel.CreateUnbounded<T>();
        int remainingChannels = channels.Length;

        foreach (Channel<T> channel in channels)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in channel.Reader.ReadAllAsync())
                    {
                        await outputChannel.Writer.WriteAsync(item);
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref remainingChannels) == 0)
                    {
                        outputChannel.Writer.Complete();
                    }
                }
            });
        }

        return outputChannel;
    }

    private async Task YieldTitle(string title)
    {
        await YieldResponse(SseResponseLine.UpdateTitle(""));
        foreach (string segment in TestChatService.UnicodeCharacterSplit(title))
        {
            await YieldResponse(SseResponseLine.TitleSegment(segment));
            await Task.Delay(10);
        }
    }

    private readonly static ReadOnlyMemory<byte> dataU8 = "data: "u8.ToArray();
    private readonly static ReadOnlyMemory<byte> lfu8 = "\r\n\r\n"u8.ToArray();

    private async Task YieldResponse(SseResponseLine line)
    {
        await Response.Body.WriteAsync(dataU8);
        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(line, JSON.JsonSerializerOptions));
        await Response.Body.WriteAsync(lfu8);
        await Response.Body.FlushAsync();
    }

    static LinkedList<MessageLiteDtoNoContent> GetMessageTree(Dictionary<long, MessageLiteDtoNoContent> existingMessages, long? fromParentId)
    {
        LinkedList<MessageLiteDtoNoContent> line = [];
        long? currentParentId = fromParentId;
        while (currentParentId != null)
        {
            if (!existingMessages.ContainsKey(currentParentId.Value))
            {
                break;
            }
            line.AddFirst(existingMessages[currentParentId.Value]);
            currentParentId = existingMessages[currentParentId.Value].ParentId;
        }
        return line;
    }

    [HttpPost("stop/{stopId}")]
    public IActionResult StopChat(string stopId)
    {
        if (stopService.TryCancel(stopId))
        {
            return Ok();
        }
        else
        {
            return NotFound();
        }
    }
}
