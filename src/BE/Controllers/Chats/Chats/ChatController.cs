using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.Test;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using EmptyResult = Microsoft.AspNetCore.Mvc.EmptyResult;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chats"), Authorize]
public class ChatController(ChatStopService stopService, AsyncClientInfoManager clientInfoManager) : ControllerBase
{
    [HttpPost("regenerate-assistant-message")]
    public async Task<IActionResult> RegenerateOneMessage(
        [FromBody] EncryptedRegenerateAssistantMessageRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
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
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService,
            cancellationToken);
    }

    [HttpPost("regenerate-all-assistant-message")]
    public async Task<IActionResult> RegenerateAllMessage(
    [FromBody] EncryptedRegenerateAllAssistantMessageRequest req,
    [FromServices] ChatsDB db,
    [FromServices] CurrentUser currentUser,
    [FromServices] ILogger<ChatController> logger,
    [FromServices] IUrlEncryptionService idEncryption,
    [FromServices] BalanceService balanceService,
    [FromServices] ChatFactory chatFactory,
    [FromServices] UserModelManager userModelManager,
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
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService,
            cancellationToken);
    }

    [HttpPost("general")]
    public async Task<IActionResult> GeneralChat(
        [FromBody] EncryptedGeneralChatRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
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
            db, currentUser, logger, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService,
            cancellationToken);
    }

    private async Task<IActionResult> ChatPrivate(
        ChatRequest req,
        ChatsDB db,
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        IUrlEncryptionService idEncryption,
        BalanceService balanceService,
        ChatFactory chatFactory,
        UserModelManager userModelManager,
        FileUrlProvider fup,
        ChatConfigService chatConfigService,
        DBFileService dbFileService,
        CancellationToken cancellationToken)
    {
        long firstTick = Stopwatch.GetTimestamp();
        cancellationToken = default; // disallow cancellation token for now for better user experience

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans).ThenInclude(x => x.ChatConfig)
            .Include(x => x.ChatTurns)
            .FirstOrDefaultAsync(x => x.Id == req.ChatId && x.UserId == currentUser.Id, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        Dictionary<long, ChatTurnLiteDto> existingMessages = chat.ChatTurns
            .Select(x => new ChatTurnLiteDto()
            {
                Id = x.Id,
                IsUser = x.IsUser,
                ParentId = x.ParentId,
                SpanId = x.SpanId,
            })
            .ToDictionary(x => x.Id, x => x);
        bool isEmptyChat = existingMessages.Count == 0;

        // ensure chat.ChatSpan contains all span ids that in request, otherwise return error
        ChatSpan[] toGenerateSpans = null!;
        if (req is RegenerateAssistantMessageRequest rr)
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
        else if (req is GeneralChatRequest or RegenerateAllAssistantMessageRequest)
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

        ChatTurn? newDbUserMessage = null;
        if (req is GeneralChatRequest generalRequest)
        {
            if (generalRequest.ParentAssistantMessageId != null)
            {
                if (!existingMessages.TryGetValue(generalRequest.ParentAssistantMessageId.Value, out ChatTurnLiteDto? parentMessage))
                {
                    return BadRequest("Invalid message id");
                }

                if (!parentMessage.IsUser)
                {
                    return BadRequest("Parent message is not assistant message");
                }
            }

            newDbUserMessage = new()
            {
                IsUser = true,
                Steps =
                [
                    new Step()
                    {
                        StepContents = await StepContent.FromRequest(generalRequest.UserMessage, fup, cancellationToken),
                        ChatRoleId = (byte)DBChatRole.User,
                        CreatedAt = DateTime.UtcNow,
                        Edited = false,
                    }
                ],
                ParentId = generalRequest.ParentAssistantMessageId,
            };
            chat.ChatTurns.Add(newDbUserMessage);
        }
        else if (req is RegenerateAllAssistantMessageRequest regenerateRequest)
        {
            if (!existingMessages.TryGetValue(regenerateRequest.ParentUserMessageId, out ChatTurnLiteDto? parentMessage))
            {
                return BadRequest("Invalid message id");
            }

            if (!parentMessage.IsUser)
            {
                return BadRequest("ParentUserMessageId is not user message");
            }
        }

        LinkedList<ChatTurnLiteDto> messageTreeNoContent = GetMessageTree(existingMessages, req.LastMessageId);
        MessageLiteDto[] messageTree = await FillContents(messageTreeNoContent, db, cancellationToken);

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Connection = "keep-alive";
        string stopId = stopService.CreateAndCombineCancellationToken(ref cancellationToken);
        await YieldResponse(SseResponseLine.CreateStopId(stopId));

        UserBalance userBalance = await db.UserBalances.Where(x => x.UserId == currentUser.Id).SingleAsync(cancellationToken);
        UserModelBalanceCalculator cost = new(BalanceInitialInfo.FromDB(userModels.Values, userBalance.Balance), []);

        Channel<SseResponseLine>[] channels = [.. toGenerateSpans.Select(x => Channel.CreateUnbounded<SseResponseLine>())];
        Dictionary<ImageChatSegment, TaskCompletionSource<DB.File>> imageFileCache = [];
        Task[] streamTasks = [.. toGenerateSpans
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
                cost.WithScoped(span.SpanId.ToString()),
                clientInfoIdTask,
                imageFileCache,
                channels[index].Writer,
                cancellationToken))];

        if (isEmptyChat && req is GeneralChatRequest generalChatRequest)
        {
            string text = generalChatRequest.UserMessage
                .OfType<TextContentRequestItem>()
                .Single()
                .Text;
            chat.Title = text[..Math.Min(50, text.Length)];
        }

        bool dbUserMessageYield = false;
        FileService fs = null!;
        await foreach (SseResponseLine line in MergeChannels(channels).Reader.ReadAllAsync(CancellationToken.None))
        {
            if (line is AllEnd allEnd)
            {
                bool isLast = allEnd.SpanId == toGenerateSpans.Last().SpanId;
                if (isLast)
                {
                    chat.LeafMessage = allEnd.Turn.Last();
                    await db.SaveChangesAsync(CancellationToken.None);
                }

                if (newDbUserMessage != null && !dbUserMessageYield)
                {
                    await YieldResponse(SseResponseLine.UserMessage(newDbUserMessage, idEncryption, fup));
                    dbUserMessageYield = true;
                }
                ChatTurn mergedMessage = ChatTurn.MergeAll(allEnd.Turn);
                await YieldResponse(SseResponseLine.ResponseMessage(allEnd.SpanId, mergedMessage, idEncryption, fup));
                if (isLast)
                {
                    await YieldResponse(SseResponseLine.ChatLeafMessageId(chat.LeafMessageId!.Value, idEncryption));
                }
            }
            else if (line is EndLine endLine)
            {
                ChatTurn message = endLine.Step;
                ChatSpan chatSpan = toGenerateSpans.Single(x => x.SpanId == message.SpanId);
                if (message.ChatRoleId != (byte)DBChatRole.ToolCall)
                {
                    message.MessageResponse!.ChatConfig = await chatConfigService.GetOrCreateChatConfig(chatSpan.ChatConfig, default);
                }
                chat.Messages.Add(message);
                chat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            else if (line is TempImageGeneratedLine tempImageGeneratedLine)
            {
                ImageChatSegment image = tempImageGeneratedLine.Image;
                if (!imageFileCache.TryGetValue(image, out TaskCompletionSource<DB.File>? tcs))
                {
                    throw new InvalidOperationException("Image file cache not found.");
                }

                await YieldResponse(SseResponseLine.ImageGenerated(tempImageGeneratedLine.SpanId, new FileDto()
                {
                    Id = Guid.NewGuid().ToString(),
                    ContentType = image.ToContentType(),
                    Url = image.ToTempUrl(),
                }));

                try
                {
                    fs ??= await FileService.GetDefault(db, cancellationToken) ?? throw new InvalidOperationException("Default file service config not found.");
                    DB.File file = await dbFileService.StoreImage(image, await clientInfoIdTask, fs, cancellationToken: default);
                    tcs.SetResult(file);
                    await YieldResponse(SseResponseLine.ImageGenerated(tempImageGeneratedLine.SpanId, fup.CreateFileDto(file, tryWithUrl: false)));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
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
        await Task.WhenAll(streamTasks);

        // finish costs
        if (cost.BalanceCost > 0)
        {
            await balanceService.UpdateBalance(db, currentUser.Id, cancellationToken);
        }
        if (cost.UsageCosts.Any())
        {
            foreach (BalanceInitialUsageInfo um in cost.UsageCosts)
            {
                if (userModels.TryGetValue(um.ModelId, out UserModel? userModel))
                {
                    await balanceService.UpdateUsage(db, userModel.Id, cancellationToken);
                }
                else
                {
                    logger.LogError("UserModel not found for model id: {modelId}", um.ModelId);
                }
            }
        }

        // yield title
        if (isEmptyChat) await YieldTitle(chat.Title);
        return new EmptyResult();
    }

    private static async Task<MessageLiteDto[]> FillContents(LinkedList<ChatTurnLiteDto> noContent, ChatsDB db, CancellationToken cancellationToken)
    {
        HashSet<long> turnIds = [.. noContent.Select(x => x.Id)];
        Dictionary<long, StepContent[]> contents = await db.StepContents
            .Where(x => turnIds.Contains(x.Step.TurnId))
            .Include(x => x.StepContentBlob)
            .Include(x => x.StepContentFile).ThenInclude(x => x!.File.FileService)
            .Include(x => x.StepContentFile).ThenInclude(x => x!.File.FileImageInfo)
            .Include(x => x.StepContentFile).ThenInclude(x => x!.File.FileContentType)
            .Include(x => x.StepContentText)
            .GroupBy(x => x.StepId)
            .ToDictionaryAsync(k => k.Key, v => v.ToArray(), cancellationToken);
        return [.. noContent.Select(x => x.WithContent(contents[x.Id]))];
    }

    private static async Task ProcessChatSpan(
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        ChatFactory chatFactory,
        FileUrlProvider fup,
        ChatSpan chatSpan,
        long firstTick,
        ChatRequest req,
        Chat chat,
        UserModel userModel,
        IEnumerable<MessageLiteDto> messageTree,
        ChatTurn? dbUserMessage,
        ScopedBalanceCalculator calc,
        Task<int> clientInfoIdTask,
        Dictionary<ImageChatSegment, TaskCompletionSource<DB.File>> imageFileCache,
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
        IMcpClient mcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
        {
            Endpoint = new Uri("https://csharp.starworks.cc/mcp"),
        }), cancellationToken: cancellationToken);
        await foreach (McpClientTool tool in mcpClient.EnumerateToolsAsync(cancellationToken: cancellationToken))
        {
            cco.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, BinaryData.FromString(tool.JsonSchema.GetRawText())));
        }

        List<ChatTurn> newMessages = [];
        while (true)
        {
            (ChatTurn message, DBFinishReason finishReason) = await RunOne(newMessages.LastOrDefault());
            await WriteMessage(message);

            if (finishReason == DBFinishReason.ToolCalls)
            {
                foreach (StepContentToolCall call in message.StepContents!
                    .Where(x => x.MessageContentToolCall != null)
                    .Select(x => x.MessageContentToolCall!))
                {
                    logger.LogInformation("Calling tool: {call.Name}, parameters: {call.Parameters}", call.Name, call.Parameters);
                    CallToolResult result = await mcpClient.CallToolAsync(call.Name, JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Parameters)!, new ProgressReporter(pnv =>
                    {
                        logger.LogInformation("Tool {call.Name} progress: {pnv.Message}", call.Name, pnv.Message);
                        writer.TryWrite(SseResponseLine.ToolProgress(chatSpan.SpanId, call.ToolCallId!, pnv.Message!));
                    }), cancellationToken: cancellationToken);
                    logger.LogInformation("Tool {call.Name} completed with result: {result}", call.Name, result.Content);
                    string toolCallResponseText = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
                    writer.TryWrite(SseResponseLine.ToolEnd(chatSpan.SpanId, call.ToolCallId!, toolCallResponseText));
                    await WriteMessage(new ChatTurn()
                    {
                        ChatId = chat.Id,
                        ChatRoleId = (byte)DBChatRole.ToolCall,
                        CreatedAt = DateTime.UtcNow,
                        Edited = false,
                        Parent = message,
                        SpanId = chatSpan.SpanId,
                        MessageContents =
                        [
                            new MessageContent()
                            {
                                MessageContentToolCallResponse = new()
                                {
                                    ToolCallId = call.ToolCallId,
                                    Response = toolCallResponseText,
                                },
                                ContentTypeId = (byte)DBMessageContentType.ToolCallResponse,
                            }
                        ],
                    });
                }
            }
            else
            {
                break;
            }
        }
        writer.TryWrite(new AllEnd() { Turn = newMessages, SpanId = chatSpan.SpanId });
        writer.Complete();

        async Task WriteMessage(ChatTurn message)
        {
            messageToSend.Add(await MessageLiteDto.FromDB(message).ToOpenAI(fup, cancellationToken));
            newMessages.Add(message);
            writer.TryWrite(SseResponseLine.End(chatSpan.SpanId, message));
        }

        async Task<(Step, DBFinishReason)> RunOne(ChatTurn? forceParentMessage)
        {
            ChatExtraDetails ced = new()
            {
                TimezoneOffset = req.TimezoneOffset,
                WebSearchEnabled = chatSpan.ChatConfig.WebSearchEnabled,
                ReasoningEffort = (DBReasoningEffort)chatSpan.ChatConfig.ReasoningEffort
            };

            InChatContext icc = new(firstTick);

            string? errorText = null;
            try
            {
                using ChatService s = chatFactory.CreateChatService(userModel.Model);

                bool responseStated = false, reasoningStarted = false;
                await foreach (InternalChatSegment seg in icc.Run(calc, userModel, s.ChatStreamedFEProcessed(messageToSend, cco, ced, cancellationToken)))
                {
                    foreach (ChatSegmentItem item in seg.Items)
                    {
                        if (item is ThinkChatSegment thinkSeg)
                        {
                            if (!reasoningStarted)
                            {
                                writer.TryWrite(SseResponseLine.StartReasoning(chatSpan.SpanId));
                                reasoningStarted = true;
                            }
                            writer.TryWrite(SseResponseLine.ReasoningSegment(chatSpan.SpanId, thinkSeg.Think));
                        }
                        else if (item is TextChatSegment textSeg)
                        {
                            if (!responseStated)
                            {
                                writer.TryWrite(SseResponseLine.StartResponse(chatSpan.SpanId, icc.ReasoningDurationMs));
                                responseStated = true;
                            }
                            writer.TryWrite(SseResponseLine.CreateSegment(chatSpan.SpanId, textSeg.Text));
                        }
                        else if (item is ToolCallSegment toolCall)
                        {
                            if (!responseStated)
                            {
                                writer.TryWrite(SseResponseLine.CallingTool(chatSpan.SpanId, toolCall.Id!, toolCall.Name!, toolCall.Arguments));
                                responseStated = true;
                            }
                        }
                        else if (item is ImageChatSegment imgSeg)
                        {
                            imageFileCache[imgSeg] = new TaskCompletionSource<DB.File>();
                            writer.TryWrite(SseResponseLine.TempImageGenerated(chatSpan.SpanId, imgSeg));
                        }
                    }

                    if (seg.FinishReason == ChatFinishReason.ContentFilter)
                    {
                        errorText = "Content Filtered";
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
            Step dbAssistantMessage = new()
            {
                ChatRoleId = (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
            };
            if (forceParentMessage != null)
            {
                dbAssistantMessage.Parent = forceParentMessage;
            }
            else if (req is GeneralChatRequest && dbUserMessage != null)
            {
                dbAssistantMessage.Parent = dbUserMessage;
            }
            else if (req is RegenerateAllAssistantMessageRequest regenerateAssistantMessageRequest)
            {
                dbAssistantMessage.ParentId = regenerateAssistantMessageRequest.ParentUserMessageId;
            }
            foreach (MessageContent mc in MessageContent.FromFullResponse(icc.FullResponse, errorText, imageFileCache))
            {
                dbAssistantMessage.MessageContents.Add(mc);
            }

            if (errorText != null)
            {
                writer.TryWrite(SseResponseLine.CreateError(chatSpan.SpanId, errorText));
            }
            UserModelUsage usage = icc.ToUserModelUsage(currentUser.Id, calc, userModel, await clientInfoIdTask, isApi: false);
            dbAssistantMessage.MessageResponse = new MessageResponse()
            {
                Usage = usage,
            };
            writer.TryWrite(SseResponseLine.End(chatSpan.SpanId, dbAssistantMessage));
            return (dbAssistantMessage, icc.FinishReason);
        }
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
            await YieldResponse(SseResponseLine.CreateTitleSegment(segment));
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

    static LinkedList<ChatTurnLiteDto> GetMessageTree(Dictionary<long, ChatTurnLiteDto> existingMessages, long? fromParentId)
    {
        LinkedList<ChatTurnLiteDto> line = [];
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
