using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Infrastructure.Functional;
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
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Security.Cryptography;
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
        WebChatRequest req,
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
                .ThenInclude(x => x.ChatConfigMcps).ThenInclude(x => x.McpServer.McpTools)
            .Include(x => x.ChatTurns)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == req.ChatId && x.UserId == currentUser.Id, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        Dictionary<long, ChatTurn> existingMessages = chat.ChatTurns.ToDictionary(x => x.Id, x => x);
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
            toGenerateSpans = [..chat.ChatSpans
                .Where(x => x.Enabled)
                .Select(x => x.Clone())];
        }
        if (toGenerateSpans.Length == 0)
        {
            return BadRequest("No enabled spans");
        }

        // validate user has access to all ChatSpan's MCP tool
        HashSet<int> mcpServerIds = [.. toGenerateSpans.SelectMany(x => x.ChatConfig.ChatConfigMcps.Select(y => y.McpServerId))];
        UserMcp[] userMcps = mcpServerIds.Count == 0 ? [] : await db.UserMcps
            .Where(x => x.UserId == currentUser.Id && mcpServerIds.Contains(x.McpServerId))
            .Include(x => x.McpServer)
            .ToArrayAsync(cancellationToken);
        if (userMcps.Length != mcpServerIds.Count)
        {
            return BadRequest("Invalid MCP server permission");
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

        ChatTurn? newDbUserTurn = null;
        if (req is GeneralChatRequest generalRequest)
        {
            if (generalRequest.ParentAssistantMessageId != null)
            {
                if (!existingMessages.TryGetValue(generalRequest.ParentAssistantMessageId.Value, out ChatTurn? parentMessage))
                {
                    return BadRequest("Invalid message id");
                }

                if (parentMessage.IsUser)
                {
                    return BadRequest("Parent message is not assistant message");
                }
            }

            newDbUserTurn = new()
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
            chat.ChatTurns.Add(newDbUserTurn);
        }
        else if (req is RegenerateAllAssistantMessageRequest regenerateRequest)
        {
            if (!existingMessages.TryGetValue(regenerateRequest.ParentUserMessageId, out ChatTurn? parentMessage))
            {
                return BadRequest("Invalid message id");
            }

            if (!parentMessage.IsUser)
            {
                return BadRequest("ParentUserMessageId is not user message");
            }
        }

        LinkedList<ChatTurn> messageTreeNoContent = GetMessageTree(existingMessages, req.LastMessageId);
        Step[] messageTree = await FillContents(messageTreeNoContent, db, cancellationToken);

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Connection = "keep-alive";
        string stopId = stopService.CreateAndCombineCancellationToken(ref cancellationToken);
        await YieldResponse(new StopIdLine(stopId));

        UserBalance userBalance = await db.UserBalances.Where(x => x.UserId == currentUser.Id).SingleAsync(cancellationToken);
        UserModelBalanceCalculator cost = new(BalanceInitialInfo.FromDB(userModels.Values, userBalance.Balance), []);

        Channel<SseResponseLine>[] channels = [.. toGenerateSpans.Select(x => Channel.CreateUnbounded<SseResponseLine>())];
        Dictionary<ImageChatSegment, TaskCompletionSource<DB.File>> imageFileCache = [];
        Task[] streamTasks = [.. toGenerateSpans.Select((span, index) => ProcessChatSpan(
            currentUser,
            logger,
            chatFactory,
            fup,
            span,
            firstTick,
            req,
            chat,
            userModels[span.ChatConfig.ModelId],
            userMcps,
            messageTree,
            newDbUserTurn,
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
            if (line is EndTurn allEnd)
            {
                bool isLast = allEnd.SpanId == toGenerateSpans.Last().SpanId;
                if (isLast)
                {
                    chat.LeafTurn = allEnd.Turn;
                }
                await db.SaveChangesAsync(CancellationToken.None);

                if (newDbUserTurn != null && !dbUserMessageYield)
                {
                    await YieldResponse(SseResponseLine.UserTurn(newDbUserTurn, idEncryption, fup));
                    dbUserMessageYield = true;
                }
                await YieldResponse(SseResponseLine.ResponseMessage(allEnd.SpanId, allEnd.Turn, idEncryption, fup));
                if (isLast)
                {
                    await YieldResponse(SseResponseLine.ChatLeafTurnId(chat.LeafTurnId!.Value, idEncryption));
                }
            }
            else if (line is EndStep endLine)
            {
                if (endLine.Step.Turn.ChatConfig == null)
                {
                    ChatSpan chatSpan = toGenerateSpans.Single(x => x.SpanId == endLine.SpanId);
                    endLine.Step.Turn.ChatConfig = await chatConfigService.GetOrCreateChatConfig(chatSpan.ChatConfig, default);
                }
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

                // yield raw temp file with data url
                //await YieldResponse(new ImageGeneratedLine(tempImageGeneratedLine.SpanId, new FileDto()
                //{
                //    Id = Guid.NewGuid().ToString(),
                //    ContentType = image.ToContentType(),
                //    Url = image.ToTempUrl(),
                //}));

                try
                {
                    fs ??= await FileService.GetDefault(db, cancellationToken) ?? throw new InvalidOperationException("Default file service config not found.");
                    DB.File file = await dbFileService.StoreImage(image, await clientInfoIdTask, fs, cancellationToken: default);
                    tcs.SetResult(file);
                    // yield final file dto
                    await YieldResponse(new ImageGeneratedLine(tempImageGeneratedLine.SpanId, fup.CreateFileDto(file, tryWithUrl: false)));
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

    private static async Task<Step[]> FillContents(LinkedList<ChatTurn> noContent, ChatsDB db, CancellationToken cancellationToken)
    {
        Dictionary<long, ChatTurn> turnMap = noContent.ToDictionary(x => x.Id, x => x);
        Dictionary<long, Step[]> contents = await db.Steps
            .Where(x => turnMap.Keys.Contains(x.TurnId))
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File.FileService)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File.FileImageInfo)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentText)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentThink)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
            .Include(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
            .OrderBy(x => x.Id)
            .GroupBy(x => x.TurnId)
            .ToDictionaryAsync(k => k.Key, v => v.ToArray(), cancellationToken);
        foreach (ChatTurn turn in noContent)
        {
            turn.Steps = contents.TryGetValue(turn.Id, out Step[]? steps) ? steps : [];
        }

        return [.. noContent.SelectMany(x => x.Steps)];
    }

    private static async Task ProcessChatSpan(
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        ChatFactory chatFactory,
        FileUrlProvider fup,
        ChatSpan chatSpan,
        long firstTick,
        WebChatRequest req,
        Chat chat,
        UserModel userModel,
        UserMcp[] userMcps,
        IEnumerable<Step> messageTree,
        ChatTurn? dbUserMessage,
        ScopedBalanceCalculator calc,
        Task<int> clientInfoIdTask,
        Dictionary<ImageChatSegment, TaskCompletionSource<DB.File>> imageFileCache,
        ChannelWriter<SseResponseLine> writer,
        CancellationToken cancellationToken)
    {
        ChatRequest csr = new()
        {
            EndUserId = currentUser.Id.ToString(),
            Steps = [.. (IEnumerable<Step>)
            [
                ..messageTree,
                ..dbUserMessage != null ? dbUserMessage.Steps : Array.Empty<Step>(),
            ]],
            ChatConfig = chatSpan.ChatConfig,
            Tools = [],
        };

        // Build a name mapping for tools to avoid collisions while keeping names clean
        Dictionary<string, (int serverId, string originalToolName)> toolNameMap = new(StringComparer.Ordinal);
        HashSet<string> usedToolNames = new(StringComparer.Ordinal);
        foreach (McpTool tool in chatSpan.ChatConfig.ChatConfigMcps.SelectMany(x => x.McpServer.McpTools))
        {
            string finalName = tool.ToolName;
            if (!usedToolNames.Add(finalName))
            {
                // Duplicate detected, generate a non-digit-leading 8-char random prefix
                string prefix;
                do
                {
                    prefix = GenerateAlphaFirstToken(8);
                    finalName = prefix + "_" + tool.ToolName;
                } while (!usedToolNames.Add(finalName));
            }

            toolNameMap[finalName] = (tool.McpServerId, tool.ToolName);
            csr.Tools.Add(ChatTool.CreateFunctionTool(finalName, tool.Description, tool.Parameters == null ? null : BinaryData.FromString(tool.Parameters)));
        }

        ChatTurn turn = new()
        {
            SpanId = chatSpan.SpanId,
            IsUser = false,
        };
        if (req is GeneralChatRequest && dbUserMessage != null)
        {
            turn.Parent = dbUserMessage;
        }
        else if (req is RegenerateAllAssistantMessageRequest regenerateAssistantMessageRequest)
        {
            turn.ParentId = regenerateAssistantMessageRequest.ParentUserMessageId;
        }
        chat.ChatTurns.Add(turn);

        while (!cancellationToken.IsCancellationRequested)
        {
            Step step = await RunOne(csr, cancellationToken);
            WriteStep(step);

            if (TryGetUnfinishedToolCall(step, out List<StepContentToolCall> unfinishedToolCalls))
            {
                foreach (StepContentToolCall call in unfinishedToolCalls)
                {
                    if (!toolNameMap.TryGetValue(call.Name!, out var mapped))
                    {
                        throw new InvalidOperationException($"Tool name not found in map: {call.Name}");
                    }
                    int serverId = mapped.serverId;
                    string toolName = mapped.originalToolName;

                    McpServer mcpServer = chatSpan.ChatConfig.ChatConfigMcps
                        .Where(x => x.McpServerId == serverId)
                        .Select(x => x.McpServer)
                        .FirstOrDefault() ?? throw new InvalidOperationException($"MCP Server not found for id: {serverId}");
                    UserMcp userMcp = userMcps.FirstOrDefault(x => x.McpServerId == mcpServer.Id)
                        ?? throw new InvalidOperationException($"UserMcp not found for server id: {mcpServer.Id}");
                    Dictionary<string, string> headers = MergeHeaders(
                        mcpServer.Headers, 
                        userMcp.CustomHeaders, 
                        chatSpan.ChatConfig.ChatConfigMcps.FirstOrDefault(x => x.McpServerId == mcpServer.Id)?.CustomHeaders);

                    logger.LogInformation("Using MCP Server {mcpServer.Label} ({mcpServer.Url}) for tool call {call.Name} with headers: {headers}",
                        mcpServer.Label, mcpServer.Url, call.Name, headers);
                    Stopwatch sw = Stopwatch.StartNew();
                    McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Endpoint = new Uri(mcpServer.Url),
                        AdditionalHeaders = headers,
                    }), cancellationToken: cancellationToken);

                    logger.LogInformation("{mcpServer.Label} connected, elapsed={elapsed}ms, Calling tool: {toolName}, parameters: {call.Parameters}",
                        mcpServer.Label, sw.ElapsedMilliseconds, toolName, call.Parameters);

                    (bool isSuccess, string toolResult) = await CallMcp(cancellationToken);
                    logger.LogInformation("Tool {call.Name} completed, success: {success}, result: {result}", call.Name, isSuccess, toolResult);
                    writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, true, call.ToolCallId!, toolResult));
                    WriteStep(new Step()
                    {
                        Turn = turn,
                        ChatRoleId = (byte)DBChatRole.ToolCall,
                        CreatedAt = DateTime.UtcNow,
                        Edited = false,
                        StepContents =
                        [
                            new StepContent()
                            {
                                StepContentToolCallResponse = new()
                                {
                                    ToolCallId = call.ToolCallId,
                                    Response = toolResult,
                                    DurationMs = (int)sw.ElapsedMilliseconds,
                                    IsSuccess = isSuccess,
                                },
                                ContentTypeId = (byte)DBStepContentType.ToolCallResponse,
                            }
                        ],
                    });

                    async Task<(bool success, string result)> CallMcp(CancellationToken cancellationToken)
                    {
                        try
                        {
                            CallToolResult result = await mcpClient.CallToolAsync(toolName, JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Parameters)!, new ProgressReporter(pnv =>
                            {
                                logger.LogInformation("Tool {call.Name} progress: {pnv.Message}", call.Name, pnv.Message);
                                writer.TryWrite(new ToolProgressLine(chatSpan.SpanId, call.ToolCallId!, pnv.Message!));
                            }), cancellationToken: cancellationToken);
                            return (result.IsError switch
                            {
                                null => true,
                                _ => !result.IsError.Value
                            }, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text)));
                        }
                        catch (McpException e)
                        {
                            return (false, e.Message);
                        }
                    }

                    Dictionary<string, string> MergeHeaders(params string?[] headers)
                    {
                        Dictionary<string, string> result = [];

                        foreach (string? header in headers)
                        {
                            if (string.IsNullOrWhiteSpace(header)) continue;
                            try
                            {
                                Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(header);
                                if (dict != null)
                                {
                                    foreach (KeyValuePair<string, string> kv in dict)
                                    {
                                        result[kv.Key] = kv.Value;
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                logger.LogWarning("Invalid MCP header JSON: {header}", header);
                            }
                        }

                        return result;
                    }
                }
            }
            else
            {
                break;
            }
        }

        static bool TryGetUnfinishedToolCall(Step step, out List<StepContentToolCall> toolCall)
        {
            toolCall = [];
            foreach (StepContent content in step.StepContents!)
            {
                if (content.ContentTypeId == (byte)DBStepContentType.ToolCall && content.StepContentToolCall != null)
                {
                    string toolCallId = content.StepContentToolCall.ToolCallId!;
                    bool hasResponse = step.StepContents.Any(x => 
                        x.ContentTypeId == (byte)DBStepContentType.ToolCallResponse 
                        && x.StepContentToolCallResponse != null
                        && x.StepContentToolCallResponse.ToolCallId == toolCallId);
                    if (!hasResponse)
                    {
                        toolCall.Add(content.StepContentToolCall);
                    }
                }
            }

            return toolCall.Count > 0;
        }

        writer.TryWrite(new EndTurn(chatSpan.SpanId, turn));
        writer.Complete();

        void WriteStep(Step step)
        {
            csr.Steps.Add(step);
            turn.Steps.Add(step);
            writer.TryWrite(new EndStep(chatSpan.SpanId, step));
        }

        async Task<Step> RunOne(ChatRequest request, CancellationToken cancellationToken)
        {
            InChatContext icc = new(firstTick);

            string? errorText = null;
            try
            {
                using ChatService s = chatFactory.CreateChatService(userModel.Model);

                bool responseStated = false, reasoningStarted = false;
                await foreach (InternalChatSegment seg in icc.Run(calc, userModel, s.ChatEntry(request, fup, UsageSource.Chat, cancellationToken)))
                {
                    foreach (ChatSegmentItem item in seg.Items)
                    {
                        if (item is ThinkChatSegment thinkSeg)
                        {
                            if (!reasoningStarted)
                            {
                                writer.TryWrite(new StartReasoningLine(chatSpan.SpanId));
                                reasoningStarted = true;
                            }
                            writer.TryWrite(new ReasoningSegmentLine(chatSpan.SpanId, thinkSeg.Think));
                        }
                        else if (item is TextChatSegment textSeg)
                        {
                            if (!responseStated)
                            {
                                writer.TryWrite(new StartResponseLine(chatSpan.SpanId, icc.ReasoningDurationMs));
                                responseStated = true;
                            }
                            writer.TryWrite(new SegmentLine(chatSpan.SpanId, textSeg.Text));
                        }
                        else if (item is ToolCallSegment toolCall)
                        {
                            if (!responseStated)
                            {
                                responseStated = true;
                            }
                            writer.TryWrite(new CallingToolLine(chatSpan.SpanId, toolCall.Id!, toolCall.Name!, toolCall.Arguments));
                        }
                        else if (item is ToolCallResponseSegment toolCallResponse)
                        {
                            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, toolCallResponse.IsSuccess, toolCallResponse.ToolCallId!, toolCallResponse.Response!));
                        }
                        else if (item is Base64PreviewImage preview)
                        {
                            writer.TryWrite(new ImageGeneratingLine(chatSpan.SpanId, preview.ToTempFileDto()));
                        }
                        else if (item is ImageChatSegment imgSeg)
                        {
                            imageFileCache[imgSeg] = new TaskCompletionSource<DB.File>();
                            writer.TryWrite(new TempImageGeneratedLine(chatSpan.SpanId, imgSeg));
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
                PipelineResponse? pr = e.GetRawResponse();
                if (pr != null)
                {
                    errorText = pr.Content.ToString();
                }
                else
                {
                    errorText = e.Message;
                }
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
                errorText = e.Message;
                logger.LogError(e, "Error in conversation for message: {userMessageId}", req.LastMessageId);
            }
            finally
            {
                // cancel the conversation because following code is credit deduction related
                cancellationToken = CancellationToken.None;
            }

            // success
            // insert new assistant message
            Step step = new()
            {
                ChatRoleId = (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Usage = icc.ToUserModelUsage(currentUser.Id, calc, userModel, await clientInfoIdTask, isApi: false),
                StepContents = [.. StepContent.FromFullResponse(icc.FullResponse, errorText, imageFileCache)],
                Turn = turn,
            };

            if (errorText != null)
            {
                writer.TryWrite(new ErrorLine(chatSpan.SpanId, errorText));
            }
            return step;
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
                    await foreach (T? item in channel.Reader.ReadAllAsync())
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
        await YieldResponse(new UpdateTitleLine(""));
        foreach (string segment in Test2ChatService.UnicodeCharacterSplit(title))
        {
            await YieldResponse(new TitleSegmentLine(segment));
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

    static LinkedList<ChatTurn> GetMessageTree(Dictionary<long, ChatTurn> existingMessages, long? fromParentId)
    {
        LinkedList<ChatTurn> line = [];
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

    private static string GenerateAlphaFirstToken(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        const string alphanum = letters + "0123456789";

        Span<char> buffer = stackalloc char[length];
        buffer[0] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
        for (int i = 1; i < length; i++)
        {
            buffer[i] = alphanum[RandomNumberGenerator.GetInt32(alphanum.Length)];
        }
        return new string(buffer);
    }
}
