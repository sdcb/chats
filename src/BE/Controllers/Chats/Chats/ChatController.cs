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

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chats"), Authorize]
public class ChatController(ChatStopService stopService) : ControllerBase
{
    [HttpPost("fresh-chat-message")]
    public async Task<IActionResult> FreshChat(
        [FromBody] FreshChatRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // ensure request span id is unique
        if (req.Spans.Select(x => x.Id).Distinct().Count() != req.Spans.Length)
        {
            return BadRequest("Duplicate span id");
        }

        return await ChatPrivate(
            req,
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, clientInfoManager, fup,
            cancellationToken);
    }

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
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        return await ChatPrivate(
            req,
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, clientInfoManager, fup,
            cancellationToken);
    }

    [HttpPost("general-chat")]
    public async Task<IActionResult> GeneralChat(
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
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (req.SpanIds.Length == 0)
        {
            return BadRequest("Spans must be provided");
        }

        foreach (int spanId in req.SpanIds)
        {
            if (spanId >= byte.MinValue && spanId <= byte.MaxValue)
            {
                continue;
            }
            return BadRequest("Invalid span id");
        }

        // ensure request span id is unique
        if (req.SpanIds.Distinct().Count() != req.SpanIds.Length)
        {
            return BadRequest("Duplicate span id");
        }

        return await ChatPrivate(
            req,
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, clientInfoManager, fup,
            cancellationToken);
    }

    private async Task<IActionResult> ChatPrivate(
        BaseChatRequest baseReq,
        ChatsDB db,
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        IUrlEncryptionService idEncryption,
        BalanceService balanceService,
        ChatFactory chatFactory,
        UserModelManager userModelManager,
        ClientInfoManager clientInfoManager,
        FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        long firstTick = Stopwatch.GetTimestamp();
        ChatRequest req = baseReq.ToChatRequest(idEncryption);

        // req.Spans should never be null
        if (req.Spans == null || req.Spans.Length == 0) return BadRequest("Spans must be provided");


        // one of message id and user message must be non-null
        if (req.MessageId == null && req.UserMessage == null)
        {
            return BadRequest("Message id or user message must be provided");
        }

        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans)
            .Include(x => x.Messages).ThenInclude(x => x.MessageContents).ThenInclude(x => x.MessageContentBlob)
            .Include(x => x.Messages).ThenInclude(x => x.MessageContents).ThenInclude(x => x.MessageContentFile)
                .ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
            .Include(x => x.Messages).ThenInclude(x => x.MessageContents).ThenInclude(x => x.MessageContentFile)
                .ThenInclude(x => x!.File).ThenInclude(x => x.FileImageInfo)
            .Include(x => x.Messages).ThenInclude(x => x.MessageContents).ThenInclude(x => x.MessageContentText)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == req.ChatId && x.UserId == currentUser.Id, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        Dictionary<long, MessageLiteDto> existingMessages = chat.Messages
            .Select(x => new MessageLiteDto()
            {
                Id = x.Id,
                Content = [.. x.MessageContents.OrderBy(x => x.Id)],
                Role = (DBChatRole)x.ChatRoleId,
                ParentId = x.ParentId,
                SpanId = x.SpanId,
            })
            .ToDictionary(x => x.Id, x => x);

        // insert system message if it doesn't exist
        bool isEmptyChat = existingMessages.Count == 0;
        if (isEmptyChat && (req.Spans == null || req.Spans.Length == 0))
        {
            return BadRequest("Empty chat must have at least one span");
        }
        MessageLiteDto[] systemMessages = GetSystemMessages(chat, req.Spans, existingMessages, isEmptyChat);

        // ensure chat.ChatSpan contains all span ids that in request, otherwise return error
        if (req.Spans.Any(x => !chat.ChatSpans.Any(y => y.SpanId == x.Id)))
        {
            return BadRequest("Invalid span id");
        }

        // apply span model update
        if (baseReq is FreshChatRequest)
        {
            foreach (ChatSpanRequest span in req.Spans)
            {
                ChatSpan chatSpan = chat.ChatSpans.First(x => x.SpanId == span.Id);
                span.ApplyTo(chatSpan);
            }
        }

        // get span id -> model id mapping but request only contains span id, so we need to get model id from chat.ChatSpan
        Dictionary<byte, short> spanModelMapping = req.Spans.ToDictionary(x => x.Id, x => x.ModelId ?? chat.ChatSpans.First(y => y.SpanId == x.Id).ModelId);
        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, [.. spanModelMapping.Values], cancellationToken);
        // ensure spanModelMapping contains all userModels
        if (spanModelMapping.Values.Any(x => !userModels.ContainsKey(x)))
        {
            return BadRequest("Invalid span model");
        }

        Message? dbUserMessage = null;
        if (req.MessageId != null)
        {
            if (!existingMessages.TryGetValue(req.MessageId.Value, out MessageLiteDto? parentMessage))
            {
                return BadRequest("Invalid message id");
            }
            else if (parentMessage.Role == DBChatRole.User)
            {
                // kind: RegenerateAssistantMessageRequest
            }
            else if (parentMessage.Role == DBChatRole.Assistant)
            {
                // kind: EditUserMessageRequest
                dbUserMessage = await MakeDbUserMessage(chat, fup, req, cancellationToken);
            }
        }
        else
        {
            // kind: NewMessageRequest
            dbUserMessage = await MakeDbUserMessage(chat, fup, req, cancellationToken);
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        string stopId = stopService.CreateAndCombineCancellationToken(ref cancellationToken);
        await YieldResponse(SseResponseLine.StopId(stopId));

        UserBalance userBalance = await db.UserBalances.Where(x => x.UserId == currentUser.Id).SingleAsync(cancellationToken);
        Task<ClientInfo> clientInfoTask = clientInfoManager.GetClientInfo(cancellationToken);

        Channel<SseResponseLine>[] channels = req.Spans.Select(x => Channel.CreateUnbounded<SseResponseLine>()).ToArray();
        Task<ChatSpanResponse>[] streamTasks = req.Spans
            .Select((span, index) => ProcessChatSpan(
                currentUser,
                logger,
                chatFactory,
                fup,
                span,
                firstTick,
                req,
                chat,
                userModels[spanModelMapping[span.Id]],
                GetMessageTree(existingMessages, req.MessageId),
                systemMessages.Where(x => x.Role == DBChatRole.System && x.SpanId == span.Id || x.SpanId == null).ToArray(),
                dbUserMessage,
                userBalance,
                clientInfoTask,
                channels[index].Writer,
                cancellationToken))
            .ToArray();

        if (isEmptyChat) chat.Title = req.UserMessage!.Text[..Math.Min(50, req.UserMessage.Text.Length)];
        bool dbUserMessageYield = false;
        await foreach (SseResponseLine line in MergeChannels(channels).Reader.ReadAllAsync(CancellationToken.None))
        {
            if (line.Kind == SseResponseKind.End)
            {
                Message dbAssistantMessage = (Message)line.Result;
                chat.Messages.Add(dbAssistantMessage);
                bool isLast = line.SpanId == req.Spans.Last().Id;
                if (isLast)
                {
                    chat.LeafMessage = dbAssistantMessage;
                }
                chat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                
                if (dbUserMessage != null && !dbUserMessageYield)
                {
                    await YieldResponse(SseResponseLine.UserMessage(dbUserMessage, idEncryption, fup));
                    dbUserMessageYield = true;
                }
                await YieldResponse(SseResponseLine.ResponseMessage(line.SpanId!.Value, dbAssistantMessage, idEncryption, fup));
                if (isLast)
                {
                    await YieldResponse(SseResponseLine.ChatLeafMessageId(chat.LeafMessageId!.Value, idEncryption));
                }
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

    private static MessageLiteDto[] GetSystemMessages(Chat chat, ChatSpanRequest[]? spans, Dictionary<long, MessageLiteDto> existingMessages, bool isEmptyChat)
    {
        if (isEmptyChat)
        {
            if (spans == null || spans.Length == 0)
            {
                throw new ArgumentException("Empty chat must have at least one span");
            }

            if (spans.Select(x => x.SystemPrompt).Distinct().Count() == 1 && spans[0].SystemPromptValid)
            {
                // only insert null spaned system message if all prompts are the same
                return [MakeSystemMessage(null, chat, spans[0])];
            }
            else
            {
                return spans
                    .Where(x => !string.IsNullOrWhiteSpace(x.SystemPrompt))
                    .Select(x => MakeSystemMessage(x.Id, chat, x))
                    .ToArray();
            }
        }
        else
        {
            return existingMessages.Values
                .Where(x => x.Role == DBChatRole.System)
                .ToArray();
        }
    }

    private static async Task<Message> MakeDbUserMessage(Chat chat, FileUrlProvider fup, ChatRequest req, CancellationToken cancellationToken)
    {
        // insert new user message
        Message? dbUserMessage = new()
        {
            ChatRoleId = (byte)DBChatRole.User,
            MessageContents = await req.UserMessage!.ToMessageContents(fup, cancellationToken),
            CreatedAt = DateTime.UtcNow,
            ParentId = req.MessageId,
        };
        chat.Messages.Add(dbUserMessage);
        return dbUserMessage;
    }

    private static async Task<ChatSpanResponse> ProcessChatSpan(
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        ChatFactory chatFactory,
        FileUrlProvider fup,
        ChatSpanRequest span,
        long firstTick,
        ChatRequest req,
        Chat chat,
        UserModel userModel,
        IEnumerable<MessageLiteDto> messageTree,
        MessageLiteDto[] systemMessages,
        Message? dbUserMessage,
        UserBalance userBalance,
        Task<ClientInfo> clientInfoTask,
        ChannelWriter<SseResponseLine> writer,
        CancellationToken cancellationToken)
    {
        OpenAIChatMessage[] messageToSend = await ((IEnumerable<MessageLiteDto>)
        [
            ..systemMessages,
            ..messageTree,
            ..(dbUserMessage != null ? [MessageLiteDto.FromDB(dbUserMessage)] : Array.Empty<MessageLiteDto>()),
        ])
        .ToAsyncEnumerable()
        .SelectAwait(async x => await x.ToOpenAI(fup, cancellationToken))
        .ToArrayAsync(cancellationToken);

        ChatSpan chatSpan = chat.ChatSpans.First(cs => cs.SpanId == span.Id);
        ChatCompletionOptions cco = span.ToChatCompletionOptions(currentUser.Id, chatSpan);
        ChatExtraDetails ced = new()
        { 
            TimezoneOffset = req.TimezoneOffset,
            WebSearchEnabled = chatSpan.EnableSearch,
        };

        InChatContext icc = new(firstTick);

        string? errorText = null;
        try
        {
            using ChatService s = chatFactory.CreateChatService(userModel.Model);
            bool responseStated = false, reasoningStarted = false;
            await foreach (InternalChatSegment seg in icc.Run(userBalance.Balance, userModel, s.ChatStreamedFEProcessed(messageToSend, cco, ced, cancellationToken)))
            {
                if (!string.IsNullOrEmpty(seg.ReasoningSegment))
                {
                    if (!reasoningStarted)
                    {
                        await writer.WriteAsync(SseResponseLine.StartReasoning(span.Id), cancellationToken);
                        reasoningStarted = true;
                    }
                    await writer.WriteAsync(SseResponseLine.ReasoningSegment(span.Id, seg.ReasoningSegment), cancellationToken);
                }
                if (!string.IsNullOrEmpty(seg.Segment))
                {
                    if (!responseStated)
                    {
                        await writer.WriteAsync(SseResponseLine.StartResponse(span.Id, icc.ReasoningDurationMs), cancellationToken);
                        responseStated = true;
                    }
                    await writer.WriteAsync(SseResponseLine.Segment(span.Id, seg.Segment), cancellationToken);
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
            logger.LogError(e, "Upstream error: {userMessageId}", req.MessageId);
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
            icc.FinishReason = DBFinishReason.InvalidApiHostUrl;
            errorText = e.Message;
            logger.LogError(e, "Invalid URL in conversation for message: {userMessageId}", req.MessageId);
        }
        catch (Exception e)
        {
            icc.FinishReason = DBFinishReason.UnknownError;
            errorText = "Unknown Error";
            logger.LogError(e, "Error in conversation for message: {userMessageId}", req.MessageId);
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
            SpanId = span.Id,
            CreatedAt = DateTime.UtcNow,
        };
        if (dbUserMessage != null)
        {
            dbAssistantMessage.Parent = dbUserMessage;
        }
        else
        {
            dbAssistantMessage.ParentId = req.MessageId;
        }
        foreach (MessageContent mc in MessageContent.FromFullResponse(icc.FullResponse, errorText))
        {
            dbAssistantMessage.MessageContents.Add(mc);
        }

        if (errorText != null)
        {
            await writer.WriteAsync(SseResponseLine.Error(span.Id, errorText), cancellationToken);
        }
        dbAssistantMessage.Usage = icc.ToUserModelUsage(currentUser.Id, userModel, await clientInfoTask, isApi: false);
        await writer.WriteAsync(new SseResponseLine { Kind = SseResponseKind.End, Result = dbAssistantMessage, SpanId = span.Id }, cancellationToken);
        writer.Complete();
        return new ChatSpanResponse()
        {
            AssistantMessage = dbAssistantMessage,
            Cost = icc.Cost,
            SpanId = span.Id,
        };
    }

    private static MessageLiteDto MakeSystemMessage(byte? specifiedSpanId, Chat chat, ChatSpanRequest span)
    {
        if (specifiedSpanId != null && specifiedSpanId != span.Id)
        {
            throw new ArgumentException("specifiedSpanId must be null or equal to span.Id");
        }

        Message toBeInsert = new()
        {
            ChatRoleId = (byte)DBChatRole.System,
            MessageContents =
            [
                MessageContent.FromContent(span.SystemPrompt!)
            ],
            CreatedAt = DateTime.UtcNow,
            SpanId = specifiedSpanId,
        };
        chat.Messages.Add(toBeInsert);
        return MessageLiteDto.FromDB(toBeInsert);
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

    static LinkedList<MessageLiteDto> GetMessageTree(Dictionary<long, MessageLiteDto> existingMessages, long? fromParentId)
    {
        LinkedList<MessageLiteDto> line = [];
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
