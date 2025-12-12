using Chats.BE.Controllers.Api.AnthropicCompatible.Dtos;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Controllers.Api.AnthropicCompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public class AnthropicMessagesController(
    ChatsDB db,
    CurrentApiKey currentApiKey,
    ChatFactory cf,
    UserModelManager userModelManager,
    ILogger<AnthropicMessagesController> logger,
    BalanceService balanceService,
    FileUrlProvider fup) : ControllerBase
{
    private static readonly DBApiType[] AllowedApiTypes = [DBApiType.OpenAIChatCompletion, DBApiType.OpenAIResponse, DBApiType.AnthropicMessages];

    [HttpPost("v1/messages")]
    public async Task<ActionResult> CreateMessage([FromBody] JsonObject json, [FromServices] AsyncClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        InChatContext icc = new(Stopwatch.GetTimestamp());
        AnthropicRequestWrapper request = new(json);

        if (!request.SeemsValid())
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, "Invalid request: model, max_tokens and messages are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, "model is required.");
        }

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, request.Model, cancellationToken);
        if (userModel == null)
        {
            return ErrorMessage(AnthropicErrorTypes.NotFoundError, $"The model `{request.Model}` does not exist or you do not have access to it.");
        }

        if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, $"The model `{request.Model}` does not support messages API.");
        }

        return await ProcessMessage(request, userModel, icc, clientInfoIdTask, cancellationToken);
    }

    private async Task<ActionResult> ProcessMessage(
        AnthropicRequestWrapper request,
        UserModel userModel,
        InChatContext icc,
        Task<int> clientInfoIdTask,
        CancellationToken cancellationToken)
    {
        Model cm = userModel.Model;
        ChatService s = cf.CreateChatService(cm);
        UserBalance userBalance = await db.UserBalances
            .Where(x => x.UserId == currentApiKey.User.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("User balance not found.");
        UserModelBalanceCalculator calc = new(BalanceInitialInfo.FromDB([userModel], userBalance.Balance), []);
        ScopedBalanceCalculator scopedCalc = calc.WithScoped("0");
        ActionResult? errorToReturn = null;
        bool hasSuccessYield = false;
        string messageId = $"msg_{Guid.NewGuid():N}";

        // Track content block state for streaming
        StreamingState streamingState = new();
        bool messageStarted = false;

        try
        {
            ChatRequest csr = request.ToChatRequest(currentApiKey.User.Id.ToString(), cm);
            await foreach (ChatSegment segment in icc.Run(scopedCalc, userModel, s, csr, fup, cancellationToken))
            {
                if (request.Streamed)
                {
                    if (!hasSuccessYield)
                    {
                        Response.StatusCode = 200;
                        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                        Response.Headers.Connection = "keep-alive";
                    }

                    if (segment is UsageChatSegment usageSegment)
                    {
                        if (!messageStarted)
                        {
                            await YieldEvent("message_start", CreateMessageStartEvent(request.Model!, messageId, usageSegment.Usage.InputTokens), cancellationToken);
                            await YieldEvent("ping", new PingEvent(), cancellationToken);
                            messageStarted = true;
                            hasSuccessYield = true;
                        }
                        continue;
                    }

                    if (!messageStarted)
                    {
                        await YieldEvent("message_start", CreateMessageStartEvent(request.Model!, messageId, 0), cancellationToken);
                        await YieldEvent("ping", new PingEvent(), cancellationToken);
                        messageStarted = true;
                        hasSuccessYield = true;
                    }

                    await ProcessStreamingItem(segment, streamingState, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
            }

            // Send final events for streaming
            if (request.Streamed && hasSuccessYield && icc.FinishReason != DBFinishReason.Cancelled)
            {
                // Close any open content block
                if (streamingState.CurrentBlockIndex >= 0)
                {
                    await YieldEvent("content_block_stop", new ContentBlockStopEvent { Index = streamingState.CurrentBlockIndex }, cancellationToken);
                }

                // Send message_delta with stop_reason
                await YieldEvent("message_delta", icc.FullResponse!.ToMessageDeltaEvent(), cancellationToken);

                // Send message_stop
                await YieldEvent("message_stop", new MessageStopEvent(), cancellationToken);
            }
        }
        catch (RawChatServiceException rawEx)
        {
            icc.FinishReason = rawEx.ErrorCode;
            logger.LogError(rawEx, "Upstream error: {StatusCode}", rawEx.StatusCode);
            errorToReturn = await YieldRawError(hasSuccessYield && request.Streamed, rawEx.Body, cancellationToken);
        }
        catch (ChatServiceException cse)
        {
            icc.FinishReason = cse.ErrorCode;
            errorToReturn = await YieldError(hasSuccessYield && request.Streamed, MapFinishReasonToErrorType(cse.ErrorCode), cse.Message, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            icc.FinishReason = DBFinishReason.Cancelled;
        }
        catch (Exception e)
        {
            icc.FinishReason = DBFinishReason.UnknownError;
            logger.LogError(e, "Unknown error");
            errorToReturn = await YieldError(hasSuccessYield && request.Streamed, AnthropicErrorTypes.ApiError, "Internal server error", cancellationToken);
        }
        finally
        {
            cancellationToken = CancellationToken.None;
        }

        // Save usage
        UserApiUsage usage = new()
        {
            ApiKeyId = currentApiKey.ApiKeyId,
            Usage = icc.ToUserModelUsage(currentApiKey.User.Id, scopedCalc, userModel, await clientInfoIdTask, isApi: true),
        };
        db.UserApiUsages.Add(usage);
        await db.SaveChangesAsync(cancellationToken);
        if (calc.BalanceCost > 0)
        {
            _ = balanceService.AsyncUpdateBalance(currentApiKey.User.Id, CancellationToken.None);
        }
        if (calc.UsageCosts.Any())
        {
            _ = balanceService.AsyncUpdateUsage([userModel!.Id], CancellationToken.None);
        }

        if (hasSuccessYield && request.Streamed)
        {
            return new EmptyResult();
        }
        else if (errorToReturn != null)
        {
            return errorToReturn;
        }
        else
        {
            // Non-streamed success response
            AnthropicResponse response = icc.FullResponse!.ToAnthropicResponse(request.Model!, messageId);
            return Ok(response);
        }
    }

    private async Task ProcessStreamingItem(ChatSegment item, StreamingState state, CancellationToken cancellationToken)
    {
        switch (item)
        {
            case ThinkChatSegment think:
                // Handle thinking blocks
                if (state.CurrentBlockType != "thinking")
                {
                    // Close previous block if any
                    if (state.CurrentBlockIndex >= 0)
                    {
                        await YieldEvent("content_block_stop", new ContentBlockStopEvent { Index = state.CurrentBlockIndex }, cancellationToken);
                    }

                    // Start new thinking block
                    state.CurrentBlockIndex++;
                    state.CurrentBlockType = "thinking";
                    await YieldEvent("content_block_start", new ContentBlockStartEvent
                    {
                        Index = state.CurrentBlockIndex,
                        ContentBlock = ContentBlockStartData.CreateThinking()
                    }, cancellationToken);
                }

                if (!string.IsNullOrEmpty(think.Think))
                {
                    await YieldEvent("content_block_delta", new ContentBlockDeltaEvent
                    {
                        Index = state.CurrentBlockIndex,
                        Delta = ContentBlockDelta.ThinkingDelta(think.Think)
                    }, cancellationToken);
                }

                if (!string.IsNullOrEmpty(think.Signature))
                {
                    await YieldEvent("content_block_delta", new ContentBlockDeltaEvent
                    {
                        Index = state.CurrentBlockIndex,
                        Delta = ContentBlockDelta.SignatureDelta(think.Signature)
                    }, cancellationToken);
                }
                break;

            case TextChatSegment text:
                // Handle text blocks
                if (state.CurrentBlockType != "text")
                {
                    // Close previous block if any
                    if (state.CurrentBlockIndex >= 0)
                    {
                        await YieldEvent("content_block_stop", new ContentBlockStopEvent { Index = state.CurrentBlockIndex }, cancellationToken);
                    }

                    // Start new text block
                    state.CurrentBlockIndex++;
                    state.CurrentBlockType = "text";
                    await YieldEvent("content_block_start", new ContentBlockStartEvent
                    {
                        Index = state.CurrentBlockIndex,
                        ContentBlock = ContentBlockStartData.CreateText()
                    }, cancellationToken);
                }

                if (!string.IsNullOrEmpty(text.Text))
                {
                    await YieldEvent("content_block_delta", new ContentBlockDeltaEvent
                    {
                        Index = state.CurrentBlockIndex,
                        Delta = ContentBlockDelta.TextDelta(text.Text)
                    }, cancellationToken);
                }
                break;

            case ToolCallSegment tool:
                // Handle tool use blocks
                string toolBlockId = $"tool_{tool.Index}";
                if (state.CurrentBlockType != toolBlockId)
                {
                    // Close previous block if any
                    if (state.CurrentBlockIndex >= 0)
                    {
                        await YieldEvent("content_block_stop", new ContentBlockStopEvent { Index = state.CurrentBlockIndex }, cancellationToken);
                    }

                    // Start new tool_use block
                    state.CurrentBlockIndex++;
                    state.CurrentBlockType = toolBlockId;

                    if (!string.IsNullOrEmpty(tool.Id) && !string.IsNullOrEmpty(tool.Name))
                    {
                        await YieldEvent("content_block_start", new ContentBlockStartEvent
                        {
                            Index = state.CurrentBlockIndex,
                            ContentBlock = ContentBlockStartData.CreateToolUse(tool.Id, tool.Name)
                        }, cancellationToken);
                    }
                }

                if (!string.IsNullOrEmpty(tool.Arguments))
                {
                    await YieldEvent("content_block_delta", new ContentBlockDeltaEvent
                    {
                        Index = state.CurrentBlockIndex,
                        Delta = ContentBlockDelta.InputJsonDelta(tool.Arguments)
                    }, cancellationToken);
                }
                break;
        }
    }

    private static MessageStartEvent CreateMessageStartEvent(string model, string messageId, int inputTokens)
    {
        return new MessageStartEvent
        {
            Message = new MessageStartData
            {
                Id = messageId,
                Model = model,
                Usage = new MessageStartUsage
                {
                    InputTokens = inputTokens
                }
            }
        };
    }

    private class StreamingState
    {
        public int CurrentBlockIndex { get; set; } = -1;
        public string? CurrentBlockType { get; set; }
    }

    private static readonly ReadOnlyMemory<byte> eventU8 = "event: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> dataU8 = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> lfU8 = "\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> lflfU8 = "\n\n"u8.ToArray();

    private async Task YieldEvent<T>(string eventType, T data, CancellationToken cancellationToken) where T : AnthropicStreamEvent
    {
        await Response.Body.WriteAsync(eventU8, cancellationToken);
        await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(eventType), cancellationToken);
        await Response.Body.WriteAsync(lfU8, cancellationToken);
        await Response.Body.WriteAsync(dataU8, cancellationToken);
        // Serialize as base type to include the type discriminator
        await JsonSerializer.SerializeAsync(Response.Body, (AnthropicStreamEvent)data, JSON.JsonSerializerOptions, cancellationToken);
        await Response.Body.WriteAsync(lflfU8, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private BadRequestObjectResult ErrorMessage(string errorType, string message)
    {
        return BadRequest(new AnthropicErrorResponse
        {
            Error = new AnthropicErrorDetail
            {
                Type = errorType,
                Message = message
            }
        });
    }

    private async Task<BadRequestObjectResult> YieldError(bool shouldStreamed, string errorType, string message, CancellationToken cancellationToken)
    {
        if (shouldStreamed)
        {
            await YieldEvent("error", new ErrorEvent
            {
                Error = new AnthropicErrorDetail
                {
                    Type = errorType,
                    Message = message
                }
            }, cancellationToken);
        }

        return ErrorMessage(errorType, message);
    }

    private async Task<ContentResult> YieldRawError(bool shouldStreamed, string rawBody, CancellationToken cancellationToken)
    {
        if (shouldStreamed)
        {
            // Send raw error as SSE event
            await Response.Body.WriteAsync(eventU8, cancellationToken);
            await Response.Body.WriteAsync("error"u8.ToArray(), cancellationToken);
            await Response.Body.WriteAsync(lfU8, cancellationToken);
            await Response.Body.WriteAsync(dataU8, cancellationToken);
            await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(rawBody), cancellationToken);
            await Response.Body.WriteAsync(lflfU8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        return new ContentResult
        {
            Content = rawBody,
            ContentType = "application/json",
            StatusCode = 400
        };
    }

    private static string MapFinishReasonToErrorType(DBFinishReason finishReason)
    {
        return finishReason switch
        {
            DBFinishReason.InvalidModel => AnthropicErrorTypes.NotFoundError,
            DBFinishReason.InsufficientBalance => AnthropicErrorTypes.PermissionError,
            DBFinishReason.SubscriptionExpired => AnthropicErrorTypes.PermissionError,
            DBFinishReason.BadParameter => AnthropicErrorTypes.InvalidRequestError,
            DBFinishReason.UpstreamError => AnthropicErrorTypes.ApiError,
            DBFinishReason.InternalConfigIssue => AnthropicErrorTypes.ApiError,
            _ => AnthropicErrorTypes.ApiError
        };
    }
}
