using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.Test;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using EmptyResult = Microsoft.AspNetCore.Mvc.EmptyResult;
using Chats.DB;
using DBFile = Chats.DB.File;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.Options;
using Microsoft.Extensions.Options;

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
        [FromServices] IOptions<ChatOptions> chatOptions,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        [FromServices] CodeInterpreterExecutor codeInterpreter,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int? retry429Times = chatOptions.Value.Retry429Times;

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, retry429Times, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter,
            cancellationToken);
    }

    [HttpPost("regenerate-all-assistant-message")]
    public async Task<IActionResult> RegenerateAllMessage(
    [FromBody] EncryptedRegenerateAllAssistantMessageRequest req,
    [FromServices] ChatsDB db,
    [FromServices] CurrentUser currentUser,
    [FromServices] ILogger<ChatController> logger,
    [FromServices] IOptions<ChatOptions> chatOptions,
    [FromServices] IUrlEncryptionService idEncryption,
    [FromServices] BalanceService balanceService,
    [FromServices] ChatFactory chatFactory,
    [FromServices] UserModelManager userModelManager,
    [FromServices] FileUrlProvider fup,
    [FromServices] ChatConfigService chatConfigService,
    [FromServices] DBFileService dBFileService,
    [FromServices] CodeInterpreterExecutor codeInterpreter,
    CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int? retry429Times = chatOptions.Value.Retry429Times;

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, retry429Times, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter,
            cancellationToken);
    }

    [HttpPost("general")]
    public async Task<IActionResult> GeneralChat(
        [FromBody] EncryptedGeneralChatRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IOptions<ChatOptions> chatOptions,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] BalanceService balanceService,
        [FromServices] ChatFactory chatFactory,
        [FromServices] UserModelManager userModelManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        [FromServices] CodeInterpreterExecutor codeInterpreter,
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

        int? retry429Times = chatOptions.Value.Retry429Times;

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, retry429Times, idEncryption, balanceService, chatFactory, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter,
            cancellationToken);
    }

    private async Task<IActionResult> ChatPrivate(
        WebChatRequest req,
        ChatsDB db,
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        int? retry429Times,
        IUrlEncryptionService idEncryption,
        BalanceService balanceService,
        ChatFactory chatFactory,
        UserModelManager userModelManager,
        FileUrlProvider fup,
        ChatConfigService chatConfigService,
        DBFileService dbFileService,
        CodeInterpreterExecutor codeInterpreter,
        CancellationToken cancellationToken)
    {
        long firstTick = Stopwatch.GetTimestamp();
        cancellationToken = default; // disallow cancellation token for now for better user experience

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans).ThenInclude(x => x.ChatConfig)
                .ThenInclude(x => x.ChatConfigMcps).ThenInclude(x => x.McpServer.McpTools)
            .Include(x => x.ChatTurns).ThenInclude(x => x.ChatDockerSessions)
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
                        StepContents = await StepContentExtensions.FromRequest(generalRequest.UserMessage, fup, cancellationToken),
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
        Dictionary<ImageChatSegment, TaskCompletionSource<DBFile>> imageFileCache = [];
        Dictionary<string, TaskCompletionSource<DBFile>> fileCache = new(StringComparer.Ordinal);
        // Ensure Model navigation is populated on the controller thread to avoid cross-thread mutation of tracked entities.
        foreach (ChatSpan span in toGenerateSpans)
        {
            span.ChatConfig.Model = userModels[span.ChatConfig.ModelId].Model;
        }

        Task[] streamTasks = [.. toGenerateSpans.Select((span, index) => ProcessChatSpan(
            currentUser,
            logger,
            chatFactory,
            fup,
            codeInterpreter,
            span,
            firstTick,
            req,
            chat,
            userModels[span.ChatConfig.ModelId],
            userMcps,
            messageTreeNoContent,
            messageTree,
            newDbUserTurn,
            cost.WithScoped(span.SpanId.ToString()),
            clientInfoIdTask,
            imageFileCache,
            fileCache,
            channels[index].Writer,
            retry429Times,
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
            if (line is TempStartTurn startTurn)
            {
                chat.ChatTurns.Add(startTurn.Turn);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            else if (line is EndTurn allEnd)
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
            else if (line is EndStepInternal endLine)
            {
                // Attach the new Step to the tracked Turn on the controller thread.
                // This avoids cross-thread mutations of EF tracked entities (DbContext is not thread-safe).
                endLine.Step.Turn.Steps.Add(endLine.Step);

                if (endLine.Step.Turn.ChatConfig == null)
                {
                    ChatSpan chatSpan = toGenerateSpans.Single(x => x.SpanId == endLine.SpanId);
                    endLine.Step.Turn.ChatConfig = await chatConfigService.GetOrCreateChatConfig(chatSpan.ChatConfig, default);
                }
                chat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                
                // Send EndStep to client with StepDto
                StepDto stepDto = StepDto.FromDB(endLine.Step, fup, idEncryption);
                await YieldResponse(new EndStep(endLine.SpanId, stepDto));
            }
            else if (line is TempImageGeneratedLine tempImageGeneratedLine)
            {
                ImageChatSegment image = tempImageGeneratedLine.Image;
                if (!imageFileCache.TryGetValue(image, out TaskCompletionSource<DBFile>? tcs))
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
                    fs ??= await db.GetDefaultFileService(cancellationToken) ?? throw new InvalidOperationException("Default file service config not found.");
                    DBFile file = await dbFileService.StoreImage(image, await clientInfoIdTask, fs, cancellationToken: default);
                    tcs.SetResult(file);
                    // yield final file dto
                    await YieldResponse(new FileGeneratedLine(tempImageGeneratedLine.SpanId, fup.CreateFileDto(file, tryWithUrl: false)));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }
            else if (line is TempFileGeneratedLine tempFileGeneratedLine)
            {
                if (!fileCache.TryGetValue(tempFileGeneratedLine.Token, out TaskCompletionSource<DBFile>? tcs))
                {
                    throw new InvalidOperationException("File cache not found.");
                }

                try
                {
                    fs ??= await db.GetDefaultFileService(cancellationToken) ?? throw new InvalidOperationException("Default file service config not found.");
                    DBFile file = await dbFileService.StoreFileBytes(
                        tempFileGeneratedLine.Bytes,
                        tempFileGeneratedLine.FileName,
                        tempFileGeneratedLine.ContentType,
                        await clientInfoIdTask,
                        fs,
                        cancellationToken);
                    tcs.SetResult(file);
                    await YieldResponse(new FileGeneratedLine(tempFileGeneratedLine.SpanId, fup.CreateFileDto(file, tryWithUrl: false)));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
                finally
                {
                    fileCache.Remove(tempFileGeneratedLine.Token);
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
        CodeInterpreterExecutor codeInterpreter,
        ChatSpan chatSpan,
        long firstTick,
        WebChatRequest req,
        Chat chat,
        UserModel userModel,
        UserMcp[] userMcps,
        IEnumerable<ChatTurn> messageTurns,
        IEnumerable<Step> messageTree,
        ChatTurn? dbUserMessage,
        ScopedBalanceCalculator calc,
        Task<int> clientInfoIdTask,
        Dictionary<ImageChatSegment, TaskCompletionSource<DBFile>> imageFileCache,
        Dictionary<string, TaskCompletionSource<DBFile>> fileCache,
        ChannelWriter<SseResponseLine> writer,
        int? retry429Times,
        CancellationToken cancellationToken)
    {
        // Combine message tree and user message steps, then convert to neutral format
        List<Step> allSteps = [.. messageTree, .. dbUserMessage?.Steps ?? []];

        bool codeExecutionEnabled = chatSpan.ChatConfig.CodeExecutionEnabled;

        IList<NeutralMessage> neutralMessages;
        if (codeExecutionEnabled)
        {
            List<NeutralMessage> injected = new(allSteps.Count);
            List<Step> priorSteps = new(allSteps.Count);

            foreach (Step step in allSteps)
            {
                NeutralMessage msg = step.ToNeutral();
                if ((DBChatRole)step.ChatRoleId == DBChatRole.User)
                {
                    string? prefix = codeInterpreter.BuildCloudFilesContextPrefix(priorSteps.Append(step));
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        List<NeutralContent> contents = [NeutralTextContent.Create(prefix), .. msg.Contents];
                        msg = msg with { Contents = contents };
                    }
                }

                injected.Add(msg);
                priorSteps.Add(step);
            }

            neutralMessages = injected;
        }
        else
        {
            neutralMessages = allSteps.ToNeutral();
        }
        ChatRequest csr = new()
        {
            EndUserId = currentUser.Id.ToString(),
            Messages = neutralMessages,
            ChatConfig = chatSpan.ChatConfig,
            System = chatSpan.ChatConfig.CodeExecutionEnabled
                ? codeInterpreter.BuildSystemMessage(chatSpan.ChatConfig.SystemPrompt, allSteps)
                : null,
            Tools = [],
            Source = UsageSource.WebChat,
        };

        // Build a name mapping for tools to avoid collisions while keeping names clean
        Dictionary<string, (int serverId, string originalToolName)> toolNameMap = new(StringComparer.Ordinal);
        HashSet<string> usedToolNames = new(StringComparer.Ordinal);

        if (codeExecutionEnabled)
        {
            // Reserve CI tool names to avoid collisions with MCP tools.
            foreach (string n in CodeInterpreterExecutor.ToolNames)
            {
                usedToolNames.Add(n);
            }
            codeInterpreter.AddTools(csr.Tools);
        }
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
            csr.Tools.Add(new FunctionTool
            {
                FunctionName = finalName,
                FunctionDescription = tool.Description,
                FunctionParameters = tool.Parameters,
            });
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

        CodeInterpreterExecutor.TurnContext? ciCtx = null;
        if (codeExecutionEnabled)
        {
            ciCtx = new CodeInterpreterExecutor.TurnContext
            {
                MessageTurns = messageTurns.Where(t => t.Id > 0).ToList(),
                MessageSteps = allSteps.ToList(),
                CurrentAssistantTurn = turn,
                ClientInfoId = await clientInfoIdTask,
            };
        }

        writer.TryWrite(new TempStartTurn(chatSpan.SpanId, turn));
        while (!cancellationToken.IsCancellationRequested)
        {
            Step step = await RunOne(csr, cancellationToken);

            bool hasUnfinishedToolCalls = TryGetUnfinishedToolCall(step, out List<StepContentToolCall> unfinishedToolCalls);

            WriteStep(step);

            if (hasUnfinishedToolCalls)
            {
                foreach (StepContentToolCall call in unfinishedToolCalls)
                {
                    string callName = call.Name ?? throw new InvalidOperationException("Tool call name is null");

                    if (codeExecutionEnabled && codeInterpreter.IsCodeInterpreterTool(callName))
                    {
                        Stopwatch ciSw = Stopwatch.StartNew();
                        bool completedSuccess = false;
                        string completedResult = "Tool did not produce completion";

                        await foreach (ToolProgressDelta delta in codeInterpreter.ExecuteToolCallAsync(
                            ciCtx!,
                            call.ToolCallId!,
                            callName,
                            call.Parameters ?? "{}",
                            cancellationToken))
                        {
                            if (delta is ToolCompletedToolProgressDelta done)
                            {
                                completedSuccess = done.Result.IsSuccess;
                                completedResult = done.Result.IsSuccess ? done.Result.Value : done.Result.Error!;
                                writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, completedSuccess, call.ToolCallId!, completedResult));
                            }
                            else
                            {
                                writer.TryWrite(new ToolProgressLine(chatSpan.SpanId, call.ToolCallId!, delta));
                            }
                        }

                        string ciResult = completedResult;
                        ciSw.Stop();

                        // Drain any artifacts produced by this tool call and upload via controller thread.
                        List<StepContent> artifactStepContents = [];
                        foreach (CodeInterpreterExecutor.PendingFileArtifact a in codeInterpreter.DrainPendingArtifacts(ciCtx!))
                        {
                            string token = $"{chatSpan.SpanId}_{Guid.NewGuid():N}";
                            TaskCompletionSource<DBFile> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                            fileCache[token] = tcs;

                            writer.TryWrite(new TempFileGeneratedLine(chatSpan.SpanId, token, a.FileName, a.ContentType, a.Bytes));

                            try
                            {
                                DBFile f = await tcs.Task;
                                artifactStepContents.Add(StepContent.FromFile(f));
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to store generated file artifact: {fileName}", a.FileName);
                            }
                        }

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
                                        Response = ciResult,
                                        DurationMs = (int)ciSw.ElapsedMilliseconds,
                                        IsSuccess = completedSuccess,
                                    },
                                    ContentTypeId = (byte)DBStepContentType.ToolCallResponse,
                                },
                                .. artifactStepContents
                            ],
                        });

                        continue;
                    }

                    if (!toolNameMap.TryGetValue(callName, out (int serverId, string originalToolName) mapped))
                    {
                        throw new InvalidOperationException($"Tool name not found in map: {callName}");
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
                            CallToolResult result = await mcpClient.CallToolAsync(toolName, JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Parameters!), new ProgressReporter(pnv =>
                            {
                                logger.LogInformation("Tool {call.Name} progress: {pnv.Message}", call.Name, pnv.Message);
                                try
                                {
                                    ToolProgressDelta delta = JsonSerializer.Deserialize<ToolProgressDelta>(pnv.Message!)!;
                                    if (delta is ToolCompletedToolProgressDelta done)
                                    {
                                        throw new Exception("ToolCompletedToolProgressDelta in mcp tool call is not supported!");
                                    }

                                    writer.TryWrite(new ToolProgressLine(chatSpan.SpanId, call.ToolCallId!, delta));
                                }
                                catch (JsonException)
                                {
                                    // ignore invalid progress delta
                                    return;
                                }
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
            firstTick = Stopwatch.GetTimestamp();
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
            csr.Messages.Add(step.ToNeutral());
            writer.TryWrite(new EndStepInternal(chatSpan.SpanId, step));
        }

        async Task<Step> RunOne(ChatRequest request, CancellationToken cancellationToken)
        {
            InChatContext icc = new(firstTick);

            string? errorText = null;
            try
            {
                ChatService s = chatFactory.CreateChatService(userModel.Model);

                bool responseStated = false, reasoningStarted = false;
                await foreach (ChatSegment segment in icc.Run(calc, userModel, s, request, fup, retry429Times, cancellationToken))
                {
                    switch (segment)
                    {
                        case ThinkChatSegment thinkSeg:
                            if (!reasoningStarted)
                            {
                                writer.TryWrite(new StartReasoningLine(chatSpan.SpanId));
                                reasoningStarted = true;
                            }
                            writer.TryWrite(new ReasoningSegmentLine(chatSpan.SpanId, thinkSeg.Think));
                            break;
                        case TextChatSegment textSeg:
                            if (!responseStated)
                            {
                                writer.TryWrite(new StartResponseLine(chatSpan.SpanId, icc.ReasoningDurationMs));
                                responseStated = true;
                            }
                            writer.TryWrite(new SegmentLine(chatSpan.SpanId, textSeg.Text));
                            break;
                        case ToolCallSegment toolCall:
                            if (!responseStated)
                            {
                                responseStated = true;
                            }
                            writer.TryWrite(new CallingToolLine(chatSpan.SpanId, toolCall.Id!, toolCall.Name!, toolCall.Arguments!));
                            break;
                        case ToolCallResponseSegment toolCallResponse:
                            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, toolCallResponse.IsSuccess, toolCallResponse.ToolCallId!, toolCallResponse.Response!));
                            break;
                        case Base64PreviewImage preview:
                            writer.TryWrite(new FileGeneratingLine(chatSpan.SpanId, preview.ToTempFileDto()));
                            break;
                        case ImageChatSegment imgSeg:
                            imageFileCache[imgSeg] = new TaskCompletionSource<DBFile>();
                            writer.TryWrite(new TempImageGeneratedLine(chatSpan.SpanId, imgSeg));
                            break;
                    }

                    if (segment is FinishReasonChatSegment finish && finish.FinishReason == DBFinishReason.ContentFilter)
                    {
                        errorText = "Content Filtered";
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                }
            }
            catch (RawChatServiceException rawEx)
            {
                icc.FinishReason = rawEx.ErrorCode;
                errorText = rawEx.Body;
                logger.LogError(rawEx, "Upstream error: {StatusCode}", rawEx.StatusCode);
            }
            catch (ChatServiceException cse)
            {
                icc.FinishReason = cse.ErrorCode;
                errorText = cse.Message;
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
                StepContents = [.. StepContentExtensions.FromFullResponse(icc.FullResponse!, errorText, imageFileCache)],
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
