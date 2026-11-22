using Chats.BE.Services.AgentRuns;
using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Controllers.Chats.Messages;
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
using Chats.BE.Services.Models.ChatServices.Anthropic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using EmptyResult = Microsoft.AspNetCore.Mvc.EmptyResult;
using Chats.DB;
using DBFile = Chats.DB.File;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Mcp;
using Chats.BE.Services.Options;
using Chats.BE.Services.RequestTracing;
using Chats.BE.Services.TitleSummary;
using Chats.BE.Services.WebsiteHosting;
using Chats.BE.Services.UserContext;
using Microsoft.Extensions.Options;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chats"), Authorize]
public class ChatController(
    ChatStopService stopService,
    AgentRunManager agentRunManager,
    ClientInfoManager clientInfoManager,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    McpToolExecutionPlanner mcpToolExecutionPlanner,
    McpToolExecutionService mcpToolExecutionService,
    WebsiteTools websiteTools) : ControllerBase
{
    private sealed record ResolvedToolCall(
        int Index,
        string ToolCallId,
        string ExposedName,
        string Parameters,
        ToolExecutionKind Kind,
        bool ReadOnly,
        McpToolExecutionRequest? McpRequest,
        string? ImmediateError = null);

    private sealed record ExecutedToolCall(
        int Index,
        string ToolCallId,
        bool IsSuccess,
        string Result,
        int DurationMs,
        IReadOnlyList<StepContent> Artifacts);

    [HttpPost("regenerate-assistant-message")]
    public async Task<IActionResult> RegenerateOneMessage(
        [FromBody] EncryptedRegenerateAssistantMessageRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] ChatRunService chatRunService,
        [FromServices] UserModelManager userModelManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        [FromServices] CodeInterpreterExecutor codeInterpreter,
        [FromServices] ChatTitleSummaryService chatTitleSummaryService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, idEncryption, chatRunService, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter, chatTitleSummaryService,
            cancellationToken);
    }

    [HttpPost("regenerate-all-assistant-message")]
    public async Task<IActionResult> RegenerateAllMessage(
    [FromBody] EncryptedRegenerateAllAssistantMessageRequest req,
    [FromServices] ChatsDB db,
    [FromServices] CurrentUser currentUser,
    [FromServices] ILogger<ChatController> logger,
    [FromServices] IUrlEncryptionService idEncryption,
    [FromServices] ChatRunService chatRunService,
    [FromServices] UserModelManager userModelManager,
    [FromServices] FileUrlProvider fup,
    [FromServices] ChatConfigService chatConfigService,
    [FromServices] DBFileService dBFileService,
    [FromServices] CodeInterpreterExecutor codeInterpreter,
    [FromServices] ChatTitleSummaryService chatTitleSummaryService,
    CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, idEncryption, chatRunService, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter, chatTitleSummaryService,
            cancellationToken);
    }

    [HttpPost("general")]
    public async Task<IActionResult> GeneralChat(
        [FromBody] EncryptedGeneralChatRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] ChatRunService chatRunService,
        [FromServices] UserModelManager userModelManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        [FromServices] CodeInterpreterExecutor codeInterpreter,
        [FromServices] ChatTitleSummaryService chatTitleSummaryService,
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
            db, currentUser, logger, idEncryption, chatRunService, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter, chatTitleSummaryService,
            cancellationToken);
    }

    /// <summary>
    /// Answers (or skips) a pending <c>ask_question</c> tool call and continues the same assistant turn in a new run.
    /// </summary>
    [HttpPost("answer-question")]
    public async Task<IActionResult> AnswerQuestion(
        [FromBody] EncryptedAnswerQuestionRequest req,
        [FromServices] ChatsDB db,
        [FromServices] CurrentUser currentUser,
        [FromServices] ILogger<ChatController> logger,
        [FromServices] IUrlEncryptionService idEncryption,
        [FromServices] ChatRunService chatRunService,
        [FromServices] UserModelManager userModelManager,
        [FromServices] FileUrlProvider fup,
        [FromServices] ChatConfigService chatConfigService,
        [FromServices] DBFileService dBFileService,
        [FromServices] CodeInterpreterExecutor codeInterpreter,
        [FromServices] ChatTitleSummaryService chatTitleSummaryService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!req.Skipped && req.Answers.Length == 0)
        {
            return BadRequest("Answers are required unless skipped");
        }

        return await ChatPrivate(
            req.Decrypt(idEncryption),
            db, currentUser, logger, idEncryption, chatRunService, userModelManager, fup, chatConfigService, dBFileService, codeInterpreter, chatTitleSummaryService,
            cancellationToken);
    }

    private async Task<IActionResult> ChatPrivate(
        WebChatRequest req,
        ChatsDB db,
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        IUrlEncryptionService idEncryption,
        ChatRunService chatRunService,
        UserModelManager userModelManager,
        FileUrlProvider fup,
        ChatConfigService chatConfigService,
        DBFileService dbFileService,
        CodeInterpreterExecutor codeInterpreter,
        ChatTitleSummaryService chatTitleSummaryService,
        CancellationToken cancellationToken)
    {
        cancellationToken = default; // disallow cancellation token for now for better user experience

        _ = clientInfoManager.GetClientInfoId(cancellationToken);
        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans).ThenInclude(x => x.ChatConfig)
                .ThenInclude(x => x.ChatConfigMcps).ThenInclude(x => x.McpServer.McpTools)
            .Include(x => x.ChatTurns).ThenInclude(x => x.ContainerResources.Where(s => s.DeletedAt == null && (s.IsPermanent || s.CleanupAt > DateTime.UtcNow)))
            .Include(x => x.ChatTurns).ThenInclude(x => x.ChatConfigSnapshot).ThenInclude(x => x!.ModelSnapshot)
            .Include(x => x.ContainerResources.Where(s => s.DeletedAt == null && (s.IsPermanent || s.CleanupAt > DateTime.UtcNow)))
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == req.ChatId && x.UserId == currentUser.Id, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        DateTime runFreshAfter = DateTime.UtcNow - AgentRunManager.HeartbeatTimeout;
        if (await db.ChatRuns.AnyAsync(x => x.ChatId == chat.Id
            && x.Status == (byte)ChatRunStatus.Running
            && x.HeartbeatAt > runFreshAfter, cancellationToken))
        {
            return Conflict("A response is still being generated in this chat.");
        }

        Dictionary<long, ChatTurn> existingMessages = chat.ChatTurns.ToDictionary(x => x.Id, x => x);
        bool isEmptyChat = existingMessages.Count == 0;

        ChatTurn? continueTurn = null;
        if (req is ContinueTurnRequest continueRequest)
        {
            if (!existingMessages.TryGetValue(continueRequest.AssistantTurnId, out continueTurn) || continueTurn.IsUser || continueTurn.SpanId == null)
            {
                return BadRequest("Invalid turn id");
            }
            continueRequest.ParentUserTurnId = continueTurn.ParentId;
        }

        async Task<long[]> GetSiblingIds(long turnId)
        {
            var rows = await db.ChatTurns
                .Where(x => x.ChatId == chat.Id && x.Steps.Any())
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.IsUser,
                    x.SpanId,
                    CreatedAt = x.Steps.Min(s => s.CreatedAt),
                })
                .ToArrayAsync(CancellationToken.None);
            ChatMessageViewService.TurnMeta[] turns = [.. rows.Select(x =>
                new ChatMessageViewService.TurnMeta(
                    x.Id, x.ParentId, x.IsUser, x.SpanId, x.CreatedAt))];
            ChatMessageViewService.TurnMeta? turn = turns.FirstOrDefault(x => x.Id == turnId);
            return turn is null ? [] : ChatMessageViewService.GetSiblingIds(turns, turn);
        }

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
        else if (continueTurn != null)
        {
            ChatSpan? span = chat.ChatSpans.FirstOrDefault(y => y.SpanId == continueTurn.SpanId);
            if (span == null)
            {
                return BadRequest("The span of this turn no longer exists");
            }
            toGenerateSpans = [span.Clone()];
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

        if (toGenerateSpans.Any(x => !x.ChatConfig.ModelId.HasValue))
        {
            return BadRequest("Model has been deleted");
        }

        Dictionary<short, UserModel> userModels = await userModelManager.GetUserModels(currentUser.Id, [.. toGenerateSpans.Select(x => x.ChatConfig.ModelId!.Value)], cancellationToken);
        {
            // ensure userModels contains all models that in toGenerateSpans
            HashSet<short> requestedModels = [.. toGenerateSpans.Select(x => x.ChatConfig.ModelId!.Value)];
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

            // Bind dangling docker sessions to the new user turn if any span has code execution enabled
            bool anyCodeExecutionEnabled = toGenerateSpans.Any(x => x.ChatConfig.CodeExecutionEnabled);
            if (anyCodeExecutionEnabled)
            {
                DateTime nowUtc = DateTime.UtcNow;
                List<ContainerResource> danglingResources = [.. chat.ContainerResources.Where(x => x.OwnerTurnId == null && !x.IsPermanent)];

                foreach (ContainerResource resource in danglingResources)
                {
                    resource.OwnerTurn = newDbUserTurn;
                    // Also add to the collection so CollectActiveSessions can find it via newDbUserTurn
                    newDbUserTurn.ContainerResources.Add(resource);
                }
            }
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

        // A new user message while the previous assistant turn is waiting for an ask_question answer:
        // close the pending call with a synthetic "skipped" response so the history stays valid.
        if (req is GeneralChatRequest && messageTreeNoContent.Last is { Value: { IsUser: false } parentAssistantTurn })
        {
            string[] pendingAskIds = GetPendingAskQuestionCalls(parentAssistantTurn.Steps).Select(x => x.ToolCallId!).ToArray();
            if (pendingAskIds.Length > 0)
            {
                Step skipStep = CreateToolResponseStep(parentAssistantTurn, pendingAskIds.Select(id => (id, AskQuestionTool.SkippedByNewMessageResponse)));
                // Steps may be a fixed-size array from FillContents; swap it for a list before EF fix-up touches it.
                parentAssistantTurn.Steps = [.. parentAssistantTurn.Steps, skipStep];
                db.Steps.Add(skipStep);
                messageTree = [.. messageTree, skipStep];
            }
        }

        Step? answerStep = null;
        if (continueTurn != null && req is ContinueTurnRequest answerRequest)
        {
            await FillContents(new LinkedList<ChatTurn>([continueTurn]), db, cancellationToken);
            continueTurn.Steps = [.. continueTurn.Steps.OrderBy(x => x.Id)];
            StepContentToolCall? askCall = GetPendingAskQuestionCalls(continueTurn.Steps)
                .FirstOrDefault(x => x.ToolCallId == answerRequest.ToolCallId);
            if (askCall == null)
            {
                return BadRequest("There is no pending question for this tool call");
            }

            AskQuestionTool.TryParse(askCall.Parameters, out AskQuestionItem[] questions, out _);
            string answerText = answerRequest.Skipped
                ? AskQuestionTool.SkippedResponse
                : AskQuestionTool.FormatAnswers(questions, answerRequest.Answers);
            answerStep = CreateToolResponseStep(continueTurn, [(answerRequest.ToolCallId, answerText)]);
            db.Steps.Add(answerStep);
            // EF fix-up may already have attached it through the Turn navigation; never send it twice.
            if (!continueTurn.Steps.Contains(answerStep))
            {
                continueTurn.Steps.Add(answerStep);
            }
        }

        if (newDbUserTurn != null)
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeSpan userOffset = TimeSpan.FromMinutes(-req.TimezoneOffset);
            DateTimeOffset userLocalTime = new DateTimeOffset(utcNow, TimeSpan.Zero).ToOffset(userOffset);

            // The model identity is already part of the provider request metadata.
            // Keep it out of the user-facing context prompt; only dynamic runtime
            // context (such as the current time and code-interpreter state) belongs here.
            List<UserContextContribution> contributions = [];
            byte[] codeInterpreterSpanIds = [.. toGenerateSpans
                .Where(x => x.ChatConfig.CodeExecutionEnabled)
                .Select(x => x.SpanId)
                .Distinct()
                .Order()];

            if (codeInterpreterSpanIds.Length > 0)
            {
                IEnumerable<ChatTurn> contextTurns = messageTreeNoContent.Append(newDbUserTurn);
                string? codeInterpreterContext = CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix(contextTurns, utcNow);
                if (!string.IsNullOrWhiteSpace(codeInterpreterContext))
                {
                    contributions.Add(new UserContextContribution(
                        "code_interpreter",
                        codeInterpreterContext,
                        codeInterpreterSpanIds));
                }
            }

            StepContentText primaryText = newDbUserTurn.Steps
                .SelectMany(x => x.StepContents)
                .Where(x => x.ContentType == DBStepContentType.Text)
                .Select(x => x.StepContentText!)
                .First();
            primaryText.ContextTemplate = UserContextTemplate.Build(userLocalTime, contributions);
        }

        ChatRunKind runKind = req switch
        {
            ContinueTurnRequest => ChatRunKind.Continue,
            RegenerateAssistantMessageRequest => ChatRunKind.RegenerateOne,
            RegenerateAllAssistantMessageRequest => ChatRunKind.RegenerateAll,
            _ => ChatRunKind.General,
        };
        DateTime runCreatedAt = DateTime.UtcNow;
        ChatRun dbRun = new()
        {
            ChatId = chat.Id,
            UserId = currentUser.Id,
            Kind = (byte)runKind,
            Status = (byte)ChatRunStatus.Running,
            InstanceId = AgentRunManager.InstanceId,
            CreatedAt = runCreatedAt,
            HeartbeatAt = runCreatedAt,
        };
        db.ChatRuns.Add(dbRun);
        // Persist the user turn (and any synthetic tool responses) together with the run so that
        // a client refreshing mid-run can rebuild the conversation from the run metadata.
        await db.SaveChangesAsync(CancellationToken.None);
        dbRun.Metadata = JSON.Serialize(new ChatRunMetadata
        {
            Mode = req.Mode,
            UserTurnId = newDbUserTurn?.Id,
            ParentAssistantTurnId = (req as GeneralChatRequest)?.ParentAssistantMessageId,
            ParentUserTurnId = (req as RegenerateAllAssistantMessageRequest)?.ParentUserMessageId ?? continueTurn?.ParentId,
            AssistantTurnId = continueTurn?.Id,
            Spans = [.. toGenerateSpans.Select(x => new ChatRunSpanInfo
            {
                SpanId = x.SpanId,
                ModelId = x.ChatConfig.ModelId!.Value,
                ModelName = userModels[x.ChatConfig.ModelId!.Value].Model.CurrentSnapshot.Name,
            })],
        });
        await db.SaveChangesAsync(CancellationToken.None);

        LiveRun liveRun = agentRunManager.Register(dbRun.Id, chat.Id, currentUser.Id);
        _liveRun = liveRun;
        ChatRunStatus finalRunStatus = ChatRunStatus.Failed;
        try
        {
            cancellationToken = liveRun.Cts.Token;
            return await RunChat();
        }
        finally
        {
            await agentRunManager.CompleteAsync(liveRun, finalRunStatus);
        }

        async Task<IActionResult> RunChat()
        {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Connection = "keep-alive";
        string stopId = stopService.CreateAndCombineCancellationToken(ref cancellationToken);
        CancellationToken runToken = cancellationToken;
        await YieldResponse(new RunStartedLine(idEncryption.EncryptChatRunId(dbRun.Id)));
        await YieldResponse(new StopIdLine(stopId));
        if (answerStep != null)
        {
            await YieldResponse(new EndStep(continueTurn!.SpanId!.Value, StepDto.FromDB(answerStep, fup, idEncryption)));
        }
        bool interactive = toGenerateSpans.Length == 1;

        List<Channel<SseResponseLine>> channels = [.. toGenerateSpans.Select(x => Channel.CreateUnbounded<SseResponseLine>())];
        Dictionary<ImageChatSegment, TaskCompletionSource<DBFile>> imageFileCache = [];
        Dictionary<string, TaskCompletionSource<DBFile>> fileCache = new(StringComparer.Ordinal);
        // Ensure Model navigation is populated on the controller thread to avoid cross-thread mutation of tracked entities.
        foreach (ChatSpan span in toGenerateSpans)
        {
            span.ChatConfig.Model = userModels[span.ChatConfig.ModelId!.Value].Model;
        }

        // Spans run concurrently with the merge loop below, which saves through `db`; give each span its own
        // DbContext scope for the code interpreter so their queries never overlap on one context instance.
        IServiceScopeFactory spanScopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        CodeInterpreterExecutor CreateSpanCodeInterpreter()
        {
            AsyncServiceScope scope = spanScopeFactory.CreateAsyncScope();
            HttpContext.Response.RegisterForDisposeAsync(scope);
            return scope.ServiceProvider.GetRequiredService<CodeInterpreterExecutor>();
        }

        List<Task> streamTasks = [.. toGenerateSpans.Select((span, index) => ProcessChatSpan(
            currentUser,
            logger,
            chatRunService,
            fup,
            span.ChatConfig.CodeExecutionEnabled ? CreateSpanCodeInterpreter() : codeInterpreter,
            span,
            req,
            chat,
            userModels[span.ChatConfig.ModelId!.Value],
            userMcps,
            messageTreeNoContent,
            messageTree,
            newDbUserTurn,
            continueTurn,
            req.Mode,
            interactive,
            dbRun.Id,
            imageFileCache,
            fileCache,
            channels[index].Writer,
            httpClientFactory,
            loggerFactory,
            cancellationToken))];

        bool hasDedicatedTitleStream = false;
        if (isEmptyChat && req is GeneralChatRequest generalChatRequest)
        {
            ChatSpan firstSpan = toGenerateSpans
                .OrderBy(x => x.SpanId)
                .First();
            TextContentRequestItem firstTextItem = generalChatRequest.UserMessage
                .OfType<TextContentRequestItem>()
                .First();
            Channel<SseResponseLine> titleChannel = Channel.CreateUnbounded<SseResponseLine>();
            channels.Add(titleChannel);
            streamTasks.Add(chatTitleSummaryService.StreamTitleAsync(
                chat.Id,
                firstSpan.ChatConfig.SystemPrompt,
                userModels[firstSpan.ChatConfig.ModelId!.Value],
                firstTextItem.Text,
                titleChannel.Writer,
                cancellationToken));
            hasDedicatedTitleStream = true;
        }

        bool dbUserMessageYield = false;
        FileService fs = null!;
        await foreach (SseResponseLine line in ReadWithKeepAlive(MergeChannels([.. channels]).Reader))
        {
            if (line is TempStartTurn startTurn)
            {
                if (!chat.ChatTurns.Contains(startTurn.Turn))
                {
                    chat.ChatTurns.Add(startTurn.Turn);
                }
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
                    long[] userSiblingIds = await GetSiblingIds(newDbUserTurn.Id);
                    await YieldResponse(SseResponseLine.UserTurn(newDbUserTurn, idEncryption, fup, userSiblingIds));
                    dbUserMessageYield = true;
                }
                long[] responseSiblingIds = await GetSiblingIds(allEnd.Turn.Id);
                await YieldResponse(SseResponseLine.ResponseMessage(allEnd.SpanId, allEnd.Turn, idEncryption, fup, responseSiblingIds));
                if (isLast)
                {
                    await YieldResponse(SseResponseLine.ChatLeafTurnId(chat.LeafTurnId!.Value, idEncryption));
                }
            }
            else if (line is EndStepInternal endLine)
            {
                await AttachAndSaveStep(endLine.SpanId, endLine.Step);

                // Send EndStep to client with StepDto
                StepDto stepDto = StepDto.FromDB(endLine.Step, fup, idEncryption);
                await YieldResponse(new EndStep(endLine.SpanId, stepDto));
            }
            else if (line is SteeringStepInternal steeringLine)
            {
                await AttachAndSaveStep(steeringLine.SpanId, steeringLine.Step);
                await agentRunManager.SetInjectedStepAsync(steeringLine.SteeringIds, steeringLine.Step.Id);
                await YieldResponse(new EndStep(steeringLine.SpanId, StepDto.FromDB(steeringLine.Step, fup, idEncryption)));
                await YieldResponse(new SteeringInjectedLine(steeringLine.SpanId, steeringLine.SteeringIds));
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
                    DBFile file = await dbFileService.StoreImage(image, await clientInfoManager.GetClientInfoId(), fs, cancellationToken: default);
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
                        await clientInfoManager.GetClientInfoId(),
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
            else if (line is SetTitleInternal setTitle)
            {
                chat.Title = setTitle.Title;
                chat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            else
            {
                await YieldResponse(line);
            }
        }

        cancellationToken = CancellationToken.None;
        bool stopped = runToken.IsCancellationRequested;
        stopService.Remove(stopId);

        // not cancellable from here
        await Task.WhenAll(streamTasks);

        // Steering messages that were never injected (stopped, or arrived after the last round) go back to the client.
        SteeringDto[] discardedSteerings = await AgentRunManager.DiscardQueuedSteeringsAsync(db, dbRun.Id);
        if (discardedSteerings.Length > 0)
        {
            await YieldResponse(new SteeringDiscardedLine(discardedSteerings));
        }

        // yield title
        if (isEmptyChat && !hasDedicatedTitleStream) await YieldTitle(chat.Title);
        finalRunStatus = stopped ? ChatRunStatus.Stopped : ChatRunStatus.Completed;
        return new EmptyResult();
        }

        async Task AttachAndSaveStep(byte spanId, Step step)
        {
            // Attach the new Step to the tracked Turn on the controller thread.
            // This avoids cross-thread mutations of EF tracked entities (DbContext is not thread-safe).
            step.Turn.Steps.Add(step);

            if (step.Turn.ChatConfigSnapshot == null)
            {
                ChatSpan chatSpan = toGenerateSpans.Single(x => x.SpanId == spanId);
                step.Turn.ChatConfigSnapshot = await chatConfigService.GetOrCreateChatConfigSnapshot(chatSpan.ChatConfig, default);
            }
            chat.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    internal static IEnumerable<StepContentToolCall> GetPendingAskQuestionCalls(IEnumerable<Step> steps)
    {
        StepContent[] contents = [.. steps.OrderBy(x => x.Id).SelectMany(x => x.StepContents)];
        HashSet<string> answered = [.. contents
            .Where(x => x.ContentTypeId == (byte)DBStepContentType.ToolCallResponse && x.StepContentToolCallResponse?.ToolCallId != null)
            .Select(x => x.StepContentToolCallResponse!.ToolCallId!)];
        return contents
            .Where(x => x.ContentTypeId == (byte)DBStepContentType.ToolCall
                && x.StepContentToolCall is { Name: AskQuestionTool.Name, ToolCallId: not null }
                && !answered.Contains(x.StepContentToolCall.ToolCallId!))
            .Select(x => x.StepContentToolCall!);
    }

    private static Step CreateToolResponseStep(ChatTurn turn, IEnumerable<(string ToolCallId, string Response)> responses)
    {
        return new Step
        {
            Turn = turn,
            ChatRoleId = (byte)DBChatRole.ToolCall,
            CreatedAt = DateTime.UtcNow,
            Edited = false,
            StepContents = [.. responses.Select(x => new StepContent
            {
                StepContentToolCallResponse = new StepContentToolCallResponse
                {
                    ToolCallId = x.ToolCallId,
                    Response = x.Response,
                    DurationMs = 0,
                    IsSuccess = true,
                },
                ContentTypeId = (byte)DBStepContentType.ToolCallResponse,
            })],
        };
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

    private async Task ProcessChatSpan(
        CurrentUser currentUser,
        ILogger<ChatController> logger,
        ChatRunService chatRunService,
        FileUrlProvider fup,
        CodeInterpreterExecutor codeInterpreter,
        ChatSpan chatSpan,
        WebChatRequest req,
        Chat chat,
        UserModel userModel,
        UserMcp[] userMcps,
        IEnumerable<ChatTurn> messageTurns,
        IEnumerable<Step> messageTree,
        ChatTurn? dbUserMessage,
        ChatTurn? continueTurn,
        string mode,
        bool interactive,
        long runId,
        Dictionary<ImageChatSegment, TaskCompletionSource<DBFile>> imageFileCache,
        Dictionary<string, TaskCompletionSource<DBFile>> fileCache,
        ChannelWriter<SseResponseLine> writer,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Combine message tree and user message steps, then convert to neutral format
        IReadOnlyList<Step> continueSteps = continueTurn == null ? [] : [.. continueTurn.Steps.OrderBy(x => x.Id)];
        List<Step> allSteps = [.. messageTree, .. dbUserMessage?.Steps ?? [], .. continueSteps];

        bool codeExecutionEnabled = chatSpan.ChatConfig.CodeExecutionEnabled;
        bool applyContextTemplate = (DBApiType)chatSpan.ChatConfig.Model!.CurrentSnapshot.ApiTypeId
            != DBApiType.OpenAIImageGeneration;

        IReadOnlyList<Step> filteredHistorySteps = RemoveNonMatchingHistoricalTurnThinkingBlocks(messageTurns, userModel.ModelId);
        IList<NeutralMessage> neutralMessages = filteredHistorySteps
            .Concat(dbUserMessage?.Steps ?? [])
            .Concat(continueSteps)
            .ToNeutral(chatSpan.SpanId, applyContextTemplate);
        NeutralSystemMessage? systemMessage = chatSpan.ChatConfig.CodeExecutionEnabled
            ? await codeInterpreter.BuildSystemMessageAsync(chatSpan.ChatConfig.SystemPrompt, chat.UserId, cancellationToken)
            : string.IsNullOrWhiteSpace(chatSpan.ChatConfig.SystemPrompt)
                ? null
                : NeutralSystemMessage.FromText(chatSpan.ChatConfig.SystemPrompt);
        McpServer[] enabledMcpServers = [.. chatSpan.ChatConfig.ChatConfigMcps
            .Select(x => x.McpServer)
            .DistinctBy(x => x.Id)];
        systemMessage = McpServerInstructionsBuilder.MergeSystemMessage(
            systemMessage,
            enabledMcpServers);
        bool askQuestionEnabled = interactive && chatSpan.ChatConfig.Model.CurrentSnapshot.AllowToolCall;
        if (mode == ChatRunModes.Plan)
        {
            string planPrompt = askQuestionEnabled
                ? AskQuestionTool.PlanModePrompt
                : AskQuestionTool.PlanModePromptWithoutTool;
            systemMessage = systemMessage == null
                ? NeutralSystemMessage.FromText(planPrompt)
                : NeutralSystemMessage.FromContents([.. systemMessage.Contents, new NeutralSystemContent { Text = planPrompt }]);
        }

        ChatRequest csr = new()
        {
            EndUserId = $"{chat.Id}-{chatSpan.SpanId}",
            Messages = neutralMessages,
            ChatConfig = chatSpan.ChatConfig,
            System = systemMessage,
            Tools = [],
            Source = UsageSource.WebChat,
        };

        // Build a stable name mapping for tools to avoid collisions while keeping names clean.
        Dictionary<string, McpTool> toolNameMap = new(StringComparer.Ordinal);
        // Website tools: only when the owner can publish somewhere and the chat sees a running permanent container.
        bool websiteToolsEnabled = chatSpan.ChatConfig.Model.CurrentSnapshot.AllowToolCall
            && await websiteTools.IsAvailableAsync(chat.UserId, chat.Id, cancellationToken);
        string[] reservedToolNames = [
            .. codeExecutionEnabled ? CodeInterpreterExecutor.ToolNames : [],
            .. websiteToolsEnabled ? WebsiteTools.ToolNames : [],
            .. askQuestionEnabled ? [AskQuestionTool.Name] : Array.Empty<string>()];

        if (codeExecutionEnabled)
        {
            codeInterpreter.AddTools(csr.Tools, chatSpan.ChatConfig.Model.CurrentSnapshot.AllowVision);
        }
        if (websiteToolsEnabled)
        {
            WebsiteTools.AddTools(csr.Tools);
        }
        if (askQuestionEnabled)
        {
            csr.Tools.Add(new FunctionTool
            {
                FunctionName = AskQuestionTool.Name,
                FunctionDescription = AskQuestionTool.Description,
                FunctionParameters = AskQuestionTool.ParametersSchema,
            });
        }
        IReadOnlyList<McpToolNameMapping> mcpToolMappings = McpToolNameMapper.Build(
            enabledMcpServers.SelectMany(x => x.McpTools),
            reservedToolNames);
        foreach (McpToolNameMapping mapping in mcpToolMappings)
        {
            McpTool tool = mapping.Tool;
            toolNameMap[mapping.ExposedName] = tool;
            csr.Tools.Add(new FunctionTool
            {
                FunctionName = mapping.ExposedName,
                FunctionDescription = tool.Description,
                FunctionParameters = tool.Parameters,
            });
        }

        ChatTurn turn = continueTurn ?? new()
        {
            SpanId = chatSpan.SpanId,
            IsUser = false,
        };
        if (continueTurn == null && req is GeneralChatRequest && dbUserMessage != null)
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
            // Include dbUserMessage so that dangling sessions bound to it can be found by EnsureSession
            List<ChatTurn> contextTurns = [.. messageTurns.Where(t => t.Id > 0)];
            if (dbUserMessage != null)
            {
                contextTurns.Add(dbUserMessage);
            }

            ciCtx = new CodeInterpreterExecutor.TurnContext
            {
                MessageTurns = contextTurns,
                MessageSteps = allSteps.ToList(),
                CurrentAssistantTurn = turn,
                ClientInfoId = await clientInfoManager.GetClientInfoId(),
            };
        }

        writer.TryWrite(new TempStartTurn(chatSpan.SpanId, turn));
        while (!cancellationToken.IsCancellationRequested)
        {
            Step step = await RunOne(csr, cancellationToken);

            bool hasUnfinishedToolCalls = TryGetUnfinishedToolCall(step, out List<StepContentToolCall> unfinishedToolCalls);

            List<ResolvedToolCall> resolvedCalls = [];
            (string ToolCallId, AskQuestionItem[] Questions)? pendingAsk = null;
            for (int index = 0; index < unfinishedToolCalls.Count; index++)
            {
                StepContentToolCall call = unfinishedToolCalls[index];
                string callName = call.Name ?? throw new InvalidOperationException("Tool call name is null");
                string callId = call.ToolCallId ?? throw new InvalidOperationException("Tool call id is null");
                string parameters = call.Parameters ?? "{}";

                if (askQuestionEnabled && callName == AskQuestionTool.Name)
                {
                    if (pendingAsk != null)
                    {
                        resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.Unknown, false, null,
                            "Only one ask_question call is allowed per response. Merge your questions into a single call."));
                    }
                    else if (!AskQuestionTool.TryParse(parameters, out AskQuestionItem[] questions, out string? askError))
                    {
                        resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.Unknown, false, null, askError));
                    }
                    else
                    {
                        pendingAsk = (callId, questions);
                    }
                    continue;
                }

                if (codeExecutionEnabled && codeInterpreter.IsCodeInterpreterTool(callName))
                {
                    resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.CodeInterpreter, false, null));
                    continue;
                }

                if (websiteToolsEnabled && WebsiteTools.IsTool(callName))
                {
                    resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.Website, WebsiteTools.IsReadOnlyTool(callName), null));
                    continue;
                }

                if (!toolNameMap.TryGetValue(callName, out McpTool? tool))
                {
                    resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.Unknown, false, null));
                    continue;
                }

                call.DisplayName = tool.Title ?? tool.ToolName;
                McpServer server = tool.McpServer;
                UserMcp userMcp = userMcps.FirstOrDefault(x => x.McpServerId == server.Id)
                    ?? throw new InvalidOperationException($"UserMcp not found for server id: {server.Id}");
                string? chatConfigHeaders = chatSpan.ChatConfig.ChatConfigMcps
                    .FirstOrDefault(x => x.McpServerId == server.Id)?.CustomHeaders;
                Dictionary<string, string> headers = MergeHeaders(
                    logger,
                    server.Headers,
                    userMcp.CustomHeaders,
                    chatConfigHeaders);
                McpToolExecutionRequest executionRequest = new(
                    server.Id,
                    server.Name,
                    server.Url,
                    headers,
                    tool.ToolName,
                    parameters,
                    tool.Idempotent);
                resolvedCalls.Add(new(index, callId, callName, parameters, ToolExecutionKind.Mcp, tool.ReadOnly, executionRequest));
            }

            WriteStep(step);

            if (hasUnfinishedToolCalls)
            {
                await using McpToolExecutionScope mcpExecutionScope = mcpToolExecutionService.CreateScope();
                IReadOnlyList<ToolExecutionBatch<ResolvedToolCall>> batches = mcpToolExecutionPlanner.Plan(
                    resolvedCalls.Select(x => new ToolExecutionPlanItem<ResolvedToolCall>(x, x.Kind, x.ReadOnly)));
                foreach (ToolExecutionBatch<ResolvedToolCall> batch in batches)
                {
                    ExecutedToolCall[] completed;
                    if (batch.IsParallel)
                    {
                        completed = await Task.WhenAll(batch.Items.Select(x => ExecuteMcpCall(x.Value, mcpExecutionScope)));
                    }
                    else
                    {
                        completed = [await ExecuteCall(batch.Items[0].Value, mcpExecutionScope)];
                    }

                    foreach (ExecutedToolCall result in completed.OrderBy(x => x.Index))
                    {
                        WriteStep(new Step
                        {
                            Turn = turn,
                            ChatRoleId = (byte)DBChatRole.ToolCall,
                            CreatedAt = DateTime.UtcNow,
                            Edited = false,
                            StepContents =
                            [
                                new StepContent
                                {
                                    StepContentToolCallResponse = new StepContentToolCallResponse
                                    {
                                        ToolCallId = result.ToolCallId,
                                        Response = result.Result,
                                        DurationMs = result.DurationMs,
                                        IsSuccess = result.IsSuccess,
                                    },
                                    ContentTypeId = (byte)DBStepContentType.ToolCallResponse,
                                },
                                .. result.Artifacts,
                            ],
                        });
                    }
                }

                if (pendingAsk != null)
                {
                    // Suspend: the run ends here and the turn waits (indefinitely) for the user's answer.
                    writer.TryWrite(new AskQuestionLine(chatSpan.SpanId, pendingAsk.Value.ToolCallId, pendingAsk.Value.Questions));
                    break;
                }

                if (interactive && !cancellationToken.IsCancellationRequested)
                {
                    await TryInjectSteeringAsync();
                }
            }
            else if (interactive && !cancellationToken.IsCancellationRequested && await TryInjectSteeringAsync())
            {
                // Steering arrived after the final answer: run one more round so the model can respond to it.
                continue;
            }
            else
            {
                break;
            }
        }

        async Task<bool> TryInjectSteeringAsync()
        {
            (long Id, string Content)[] claimed = await agentRunManager.ClaimQueuedSteeringsAsync(runId, cancellationToken);
            if (claimed.Length == 0) return false;

            Step steeringStep = new()
            {
                Turn = turn,
                ChatRoleId = (byte)DBChatRole.User,
                CreatedAt = DateTime.UtcNow,
                Edited = false,
                StepContents = [StepContent.FromText(string.Join("\n\n", claimed.Select(x => x.Content)))],
            };
            csr.Messages.Add(steeringStep.ToNeutral());
            writer.TryWrite(new SteeringStepInternal(chatSpan.SpanId, steeringStep, [.. claimed.Select(x => x.Id)]));
            return true;
        }

        async Task<ExecutedToolCall> ExecuteCall(ResolvedToolCall call, McpToolExecutionScope mcpExecutionScope)
        {
            return call.Kind switch
            {
                ToolExecutionKind.Mcp => await ExecuteMcpCall(call, mcpExecutionScope),
                ToolExecutionKind.CodeInterpreter => await ExecuteCodeInterpreterCall(call),
                ToolExecutionKind.Website => await ExecuteWebsiteCall(call),
                _ => CompleteUnknownCall(call),
            };
        }

        async Task<ExecutedToolCall> ExecuteMcpCall(
            ResolvedToolCall call,
            McpToolExecutionScope mcpExecutionScope)
        {
            McpToolExecutionRequest request = call.McpRequest
                ?? throw new InvalidOperationException("Resolved MCP call has no execution request");
            logger.LogInformation(
                "Calling MCP Server {ServerName} ({ServerUrl}) tool {ToolName}",
                request.ServerName,
                request.ServerUrl,
                request.ToolName);
            McpToolExecutionResult result = await mcpToolExecutionService.ExecuteAsync(
                mcpExecutionScope,
                request,
                delta => writer.TryWrite(new ToolProgressLine(chatSpan.SpanId, call.ToolCallId, delta)),
                cancellationToken);
            logger.LogInformation(
                "MCP tool {ExposedName} completed, success={Success}, attempts={Attempts}, duration={DurationMs}ms",
                call.ExposedName,
                result.IsSuccess,
                result.Attempts,
                result.DurationMs);
            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, result.IsSuccess, call.ToolCallId, result.Result));
            return new(call.Index, call.ToolCallId, result.IsSuccess, result.Result, result.DurationMs, []);
        }

        async Task<ExecutedToolCall> ExecuteCodeInterpreterCall(ResolvedToolCall call)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = false;
            string result = "Tool did not produce completion";
            await foreach (ToolProgressDelta delta in codeInterpreter.ExecuteToolCallAsync(
                ciCtx!,
                call.ToolCallId,
                call.ExposedName,
                call.Parameters,
                cancellationToken))
            {
                if (delta is ToolCompletedToolProgressDelta done)
                {
                    success = done.Result.IsSuccess;
                    result = done.Result.IsSuccess ? done.Result.Value : done.Result.Error!;
                }
                else
                {
                    writer.TryWrite(new ToolProgressLine(chatSpan.SpanId, call.ToolCallId, delta));
                }
            }

            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, success, call.ToolCallId, result));
            List<StepContent> artifacts = [];
            foreach (CodeInterpreterExecutor.PendingFileArtifact artifact in codeInterpreter.DrainPendingArtifacts(ciCtx!))
            {
                string token = $"{chatSpan.SpanId}_{Guid.NewGuid():N}";
                TaskCompletionSource<DBFile> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                fileCache[token] = tcs;
                writer.TryWrite(new TempFileGeneratedLine(
                    chatSpan.SpanId,
                    token,
                    artifact.FileName,
                    artifact.ContentType,
                    artifact.Bytes));
                try
                {
                    artifacts.Add(StepContent.FromFile(await tcs.Task));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to store generated file artifact: {FileName}", artifact.FileName);
                }
            }

            return new(call.Index, call.ToolCallId, success, result, (int)stopwatch.ElapsedMilliseconds, artifacts);
        }

        async Task<ExecutedToolCall> ExecuteWebsiteCall(ResolvedToolCall call)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            (bool success, string result) = await websiteTools.ExecuteAsync(chat.UserId, chat.Id, call.ExposedName, call.Parameters, cancellationToken);
            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, success, call.ToolCallId, result));
            return new(call.Index, call.ToolCallId, success, result, (int)stopwatch.ElapsedMilliseconds, []);
        }

        ExecutedToolCall CompleteUnknownCall(ResolvedToolCall call)
        {
            string result = call.ImmediateError ?? $"Unknown tool: {call.ExposedName}";
            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, false, call.ToolCallId, result));
            return new(call.Index, call.ToolCallId, false, result, 0, []);
        }

        static Dictionary<string, string> MergeHeaders(ILogger logger, params string?[] sources)
        {
            Dictionary<string, string> result = [];
            foreach (string? source in sources)
            {
                if (string.IsNullOrWhiteSpace(source)) continue;
                try
                {
                    Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(source);
                    if (values is null) continue;
                    foreach (KeyValuePair<string, string> value in values)
                    {
                        result[value.Key] = value.Value;
                    }
                }
                catch (JsonException)
                {
                    logger.LogWarning("Invalid MCP header JSON: {Header}", source);
                }
            }

            return result;
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
            csr.Messages.Add(step.ToNeutral(chatSpan.SpanId, applyContextTemplate));
            writer.TryWrite(new EndStepInternal(chatSpan.SpanId, step));
        }

        async Task<Step> RunOne(ChatRequest request, CancellationToken cancellationToken)
        {
            string? errorText = null;
            bool responseStated = false;
            bool reasoningStarted = false;
            HashSet<string> hostedWebSearchCallIds = new(StringComparer.Ordinal);
            ChatRunResult runResult = await chatRunService.RunAsync(
                new ChatRunRequest
                {
                    UserModel = userModel,
                    ChatRequest = request,
                },
                async (segmentContext, ct) =>
                {
                    ChatSegment segment = segmentContext.Segment;
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
                                writer.TryWrite(new StartResponseLine(chatSpan.SpanId, segmentContext.ReasoningDurationMs));
                                responseStated = true;
                            }
                            writer.TryWrite(new SegmentLine(chatSpan.SpanId, textSeg.Text));
                            break;
                        case ToolCallSegment toolCall:
                            if (!responseStated)
                            {
                                responseStated = true;
                            }
                            string toolArguments = toolCall.Arguments!;
                            if (toolCall.Name == DeepSeekHostedWebSearch.InternalToolName
                                && toolCall.Id != null
                                && DeepSeekHostedWebSearch.TryCreatePresentationCall(toolArguments, out string presentationCall))
                            {
                                hostedWebSearchCallIds.Add(toolCall.Id);
                                toolArguments = presentationCall;
                            }
                            string? displayName = toolCall.Name is not null
                                && toolNameMap.TryGetValue(toolCall.Name, out McpTool? streamedMcpTool)
                                ? streamedMcpTool.Title ?? streamedMcpTool.ToolName
                                : null;
                            writer.TryWrite(new CallingToolLine(
                                chatSpan.SpanId,
                                toolCall.Id!,
                                toolCall.Name!,
                                toolArguments,
                                displayName,
                                toolCall.IsCompleted));
                            break;
                        case ToolCallResponseSegment toolCallResponse:
                            string toolResponse = toolCallResponse.Response!;
                            if (hostedWebSearchCallIds.Contains(toolCallResponse.ToolCallId)
                                && DeepSeekHostedWebSearch.TryCreatePresentationResponse(toolResponse, out string presentationResponse))
                            {
                                toolResponse = presentationResponse;
                            }
                            writer.TryWrite(new ToolCompletedLine(chatSpan.SpanId, toolCallResponse.IsSuccess, toolCallResponse.ToolCallId!, toolResponse));
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

                    if (ct.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    await Task.CompletedTask;
                },
                cancellationToken);

            switch (runResult.Exception)
            {
                case RawChatServiceException rawEx:
                    errorText = rawEx.Body;
                    logger.LogError(rawEx, "Upstream error: {StatusCode}", rawEx.StatusCode);
                    break;
                case ChatServiceException cse:
                    errorText = cse.Message;
                    break;
                case AggregateException e when (e.InnerException is TaskCanceledException):
                    errorText = e.InnerException.ToString();
                    break;
                case TaskCanceledException:
                    errorText = "Conversation cancelled";
                    break;
                case Exception e:
                    errorText = e.Message;
                    logger.LogError(e, "Error in conversation for message: {userMessageId}", req.LastMessageId);
                    break;
            }

            Step step = new()
            {
                ChatRoleId = (byte)DBChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                UsageId = runResult.UserModelUsageId,
                StepContents = [.. StepContentExtensions.FromFullResponse(runResult.FullResponse, errorText, imageFileCache)],
                Turn = turn,
            };

            if (errorText != null)
            {
                writer.TryWrite(new ErrorLine(chatSpan.SpanId, errorText));
            }
            return step;
        }
    }

    internal static IReadOnlyList<Step> RemoveNonMatchingHistoricalTurnThinkingBlocks(IEnumerable<ChatTurn> historyTurns, short currentModelId)
    {
        ChatTurn[] turns = [.. historyTurns];
        HashSet<long> preservedAssistantTurnIds = [];
        bool stillInSameModelSuffix = true;

        for (int i = turns.Length - 1; i >= 0; i--)
        {
            ChatTurn turn = turns[i];
            if (turn.IsUser)
            {
                continue;
            }

            short? turnModelId = turn.ChatConfigSnapshot?.ModelSnapshot.ModelId;
            if (stillInSameModelSuffix && turnModelId == currentModelId)
            {
                preservedAssistantTurnIds.Add(turn.Id);
                continue;
            }

            // 只保留历史末尾连续使用当前模型的 assistant turn 的思考信息；
            // 一旦遇到不同模型，说明上游思考上下文已经断开，更早的 thinking/signature 都不能继续带给当前模型。
            stillInSameModelSuffix = false;
        }

        List<Step> result = [];
        foreach (ChatTurn turn in turns)
        {
            bool preserveThinking = turn.IsUser || preservedAssistantTurnIds.Contains(turn.Id);
            foreach (Step step in turn.Steps.OrderBy(s => s.Id))
            {
                result.Add(preserveThinking ? step : RemoveThinkingBlocks(step));
            }
        }

        return result;

        static Step RemoveThinkingBlocks(Step step)
        {
            if (!step.StepContents.Any(c => c.ContentType == DBStepContentType.Think))
            {
                return step;
            }

            Step clone = step.WithNoMessage();
            foreach (StepContent content in step.StepContents.Where(c => c.ContentType != DBStepContentType.Think))
            {
                clone.StepContents.Add(content.Clone());
            }
            return clone;
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
    private LiveRun? _liveRun;
    private bool _clientGone;

    /// <summary>
    /// Records the event in the current run (for resuming clients) and streams it to the original client if still connected.
    /// The run keeps executing after the client disconnects.
    /// </summary>
    /// <summary>Reads merged lines, sending an SSE comment to the original client whenever nothing arrives for 15 seconds (long silent tool calls).</summary>
    private async IAsyncEnumerable<SseResponseLine> ReadWithKeepAlive(ChannelReader<SseResponseLine> reader)
    {
        while (true)
        {
            Task<bool> wait = reader.WaitToReadAsync().AsTask();
            while (await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(15))) != wait)
            {
                if (_clientGone) continue;
                try
                {
                    await Response.Body.WriteAsync(": keep-alive\r\n\r\n"u8.ToArray());
                    await Response.Body.FlushAsync();
                }
                catch (Exception)
                {
                    _clientGone = true;
                }
            }
            if (!await wait) yield break;
            while (reader.TryRead(out SseResponseLine? line)) yield return line;
        }
    }
    private async Task YieldResponse(SseResponseLine line)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(line, JSON.JsonSerializerOptions);
        int? seq = _liveRun?.Append(System.Text.Encoding.UTF8.GetString(json));
        if (_clientGone) return;

        try
        {
            if (seq != null)
            {
                await Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"id: {seq}\r\n"));
            }
            await Response.Body.WriteAsync(dataU8);
            await Response.Body.WriteAsync(json);
            await Response.Body.WriteAsync(lfu8);
            await Response.Body.FlushAsync();
        }
        catch (Exception) when (HttpContext.RequestAborted.IsCancellationRequested || _liveRun != null)
        {
            _clientGone = true;
        }
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

}
