using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Chats.BE.Services.Models.ChatServices;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
using Chats.BE.Services.FileServices;
using Chats.BE.Controllers.Api.OpenAICompatible.Dtos;
using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services.Options;
using Microsoft.Extensions.Options;

namespace Chats.BE.Controllers.Api.OpenAICompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public partial class OpenAIChatCompletionController(
    ChatsDB db,
    CurrentApiKey currentApiKey,
    ChatRunService chatRunService,
    UserModelManager userModelManager,
    ILogger<OpenAIChatCompletionController> logger,
    AsyncCacheUsageManager asyncCacheUsageService,
    ClientInfoManager clientInfoManager) : ControllerBase
{
    private static readonly DBApiType[] AllowedApiTypes = [DBApiType.OpenAIChatCompletion, DBApiType.OpenAIResponse, DBApiType.AnthropicMessages];

    [HttpPost("v1/chat/completions")]
    public async Task<ActionResult> ChatCompletion(
        [FromBody] JsonObject json,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        CcoWrapper cco = new(json);
        if (!cco.SeemsValid())
        {
            return ErrorMessage(DBFinishReason.BadParameter, "bad parameter.");
        }
        if (string.IsNullOrWhiteSpace(cco.Model))
        {
            return InvalidModel(cco.Model);
        }

        _ = clientInfoManager.GetClientInfoId(cancellationToken);
        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, cco.Model, cancellationToken);
        if (userModel == null) return InvalidModel(cco.Model);

        if (!AllowedApiTypes.Contains((DBApiType)userModel.Model.CurrentSnapshot.ApiTypeId))
        {
            return ErrorMessage(DBFinishReason.BadParameter, $"The model `{cco.Model}` does not support chat completions API.");
        }

        CcoCacheControl? ccoCacheControl = cco.CacheControl;
        if (ccoCacheControl != null)
        {
            cco.CacheControl = null;
            (ActionResult result, _) = await ChatCompletionUseCache(ccoCacheControl, cco, userModel, cancellationToken);
            return result;
        }

        (ActionResult resultNoCache, _) = await ChatCompletionNoCache(cco, userModel, cancellationToken);
        return resultNoCache;
    }

    [HttpPost("v1-cached/chat/completions")]
    [HttpPost("v1-cached-createOnly/chat/completions")]
    public async Task<ActionResult> ChatCompletionCached(
        [FromBody] JsonObject json,
        CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        DBFinishReason finishReason = DBFinishReason.Success;
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], Started", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds);
            }
            CcoWrapper cco = new(json);
            if (!cco.SeemsValid())
            {
                return ErrorMessage(DBFinishReason.BadParameter, "bad parameter.");
            }
            if (string.IsNullOrWhiteSpace(cco.Model))
            {
                return InvalidModel(cco.Model);
            }

            _ = clientInfoManager.GetClientInfoId(cancellationToken);
            UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, cco.Model, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], GetUserModel", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds);
            }
            if (userModel == null) return InvalidModel(cco.Model);

            if (!AllowedApiTypes.Contains((DBApiType)userModel.Model.CurrentSnapshot.ApiTypeId))
            {
                return ErrorMessage(DBFinishReason.BadParameter, $"The model `{cco.Model}` does not support chat completions API.");
            }

            CcoCacheControl ccoCacheControl = cco.CacheControl ?? CcoCacheControl.StaticCached with
            {
                CreateOnly = Request.GetDisplayUrl().Contains("v1-cached-createOnly", StringComparison.OrdinalIgnoreCase),
            };
            cco.CacheControl = null;
            (ActionResult result, ChatRunResult? runResult) = await ChatCompletionUseCache(ccoCacheControl, cco, userModel, cancellationToken);
            finishReason = runResult?.FinishReason ?? DBFinishReason.Success;
            return result;
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], Finish Reason: {FinishReason}", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds, finishReason.ToString());
            }
        }
    }

    private async Task<(ActionResult Result, ChatRunResult? RunResult)> ChatCompletionUseCache(
        CcoCacheControl cacheControl,
        CcoWrapper cco,
        UserModel userModel,
        CancellationToken cancellationToken)
    {
        string requestBody = cco.Serialize();
        long requestHashCode = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
        Stopwatch sw = Stopwatch.StartNew();
        logger.LogInformation("{RequestId} [{Elapsed}], Check Cache", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds);

        if (!cacheControl.CreateOnly)
        {
            UserApiCache? cache = await db.UserApiCaches
                .Include(x => x.UserApiCacheBody)
                .Include(x => x.UserApiCacheUsages)
                .Where(x => x.UserApiKeyId == currentApiKey.ApiKeyId && x.RequestHashCode == requestHashCode && x.Expires > DateTime.UtcNow && x.UserApiCacheBody!.Request == requestBody)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], Cache Found: {CacheFound}", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds, cache != null);
            }

            if (cache != null)
            {
                bool isSuccess = false;
                try
                {
                    FullChatCompletion fullResponse = JsonSerializer.Deserialize<FullChatCompletion>(cache.UserApiCacheBody!.Response)!;
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("{RequestId} [{Elapsed}], Cache Deserialized", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds);
                    }

                    if (cco.Streamed)
                    {
                        Response.StatusCode = 200;
                        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                        Response.Headers.Connection = "keep-alive";
                        if (fullResponse.Choices != null && fullResponse.Choices.Count > 0)
                        {
                            foreach (ChatSegment seg in fullResponse.Choices[0].Message.Segments)
                            {
                                await YieldResponse(seg.ToOpenAIChatCompletionChunk(fullResponse.Model, fullResponse.Id, fullResponse.SystemFingerprint), cancellationToken);
                            }
                            await YieldResponse(fullResponse.ToFinalChunk(), cancellationToken);
                        }
                        // 发送 [DONE] 标记
                        await Response.Body.WriteAsync("data: [DONE]\n\n"u8.ToArray(), cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        isSuccess = true;
                        return (Empty, null);
                    }
                    else
                    {
                        isSuccess = true;
                        return (Content(fullResponse.SerializeForApi(), "application/json"), null);
                    }
                }
                catch (JsonException e)
                {
                    logger.LogError(e, "Invalid JSON in cache");
                }
                catch (NotSupportedException e)
                {
                    // 旧缓存数据可能缺少多态类型标识符，需要重新生成
                    logger.LogWarning(e, "Cache data missing polymorphic type discriminator, regenerating");
                }
                finally
                {
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("{RequestId} [{Elapsed}], Response completed", HttpContext.TraceIdentifier, sw.ElapsedMilliseconds);
                    }
                    if (isSuccess)
                    {
                        _ = asyncCacheUsageService.SaveCacheUsage(new UserApiCacheUsage()
                        {
                            ClientInfoId = await clientInfoManager.GetClientInfoId(),
                            UsedAt = DateTime.UtcNow,
                            UserApiCacheId = cache.Id,
                        }, default);
                    }
                }
            }
        }

        (ActionResult result, ChatRunResult runResult) = await ChatCompletionNoCache(cco, userModel, cancellationToken);
        if (runResult.FinishReason == DBFinishReason.Success || runResult.FinishReason == DBFinishReason.Stop || runResult.FinishReason == DBFinishReason.ToolCalls)
        {
            FullChatCompletion toBeCached = runResult.FullResponse.ToOpenAIFullChat(cco.Model, HttpContext.TraceIdentifier);
            UserApiCache cache = new()
            {
                UserApiKeyId = currentApiKey.ApiKeyId,
                RequestHashCode = requestHashCode,
                Expires = cacheControl.ExpiresAt,
                UserApiCacheBody = new UserApiCacheBody()
                {
                    Request = requestBody,
                    Response = toBeCached.SerializeForCache(),
                },
                CreatedAt = DateTime.UtcNow,
                ClientInfoId = await clientInfoManager.GetClientInfoId(),
                ModelId = userModel.ModelId,
            };
            db.UserApiCaches.Add(cache);
            await db.SaveChangesAsync(cancellationToken);
        }

        return (result, runResult);
    }

    private async Task<(ActionResult Result, ChatRunResult RunResult)> ChatCompletionNoCache(CcoWrapper cco, UserModel userModel, CancellationToken cancellationToken)
    {
        ActionResult? errorToReturn = null;
        bool hasSuccessYield = false;
        bool streamedFinishSegment = false;
        ChatRequest csr = ChatRequest.FromOpenAI(currentApiKey.User.Id.ToString(), userModel.Model, cco.Streamed, cco.Messages!, cco.ToCleanCco());
        ChatRunResult runResult = await chatRunService.RunAsync(
            new ChatRunRequest
            {
                UserModel = userModel,
                ChatRequest = csr,
            },
            async (segmentContext, ct) =>
            {
                if (cco.Streamed)
                {
                    if (!hasSuccessYield)
                    {
                        Response.StatusCode = 200;
                        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                        Response.Headers.Connection = "keep-alive";
                    }

                    ChatCompletionChunk chunk = segmentContext.Segment.ToOpenAIChatCompletionChunk(cco.Model, HttpContext.TraceIdentifier, null);
                    await YieldResponse(chunk, ct);
                    hasSuccessYield = true;
                    streamedFinishSegment |= segmentContext.Segment is FinishReasonChatSegment;
                }

                if (ct.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
            },
            cancellationToken);

        switch (runResult.Exception)
        {
            case RawChatServiceException rawEx:
                logger.LogError(rawEx, "Upstream error: {StatusCode}", rawEx.StatusCode);
                errorToReturn = await YieldRawError(hasSuccessYield && cco.Streamed, rawEx.StatusCode, rawEx.Body, cancellationToken);
                break;
            case ChatServiceException cse:
                errorToReturn = await YieldError(hasSuccessYield && cco.Streamed, cse.ErrorCode, cse.Message, cancellationToken);
                break;
            case Exception e when e is not TaskCanceledException:
                logger.LogError(e, "Unknown error");
                errorToReturn = await YieldError(hasSuccessYield && cco.Streamed, runResult.FinishReason, "", cancellationToken);
                break;
        }

        if (cco.Streamed && hasSuccessYield && runResult.FinishReason != DBFinishReason.Cancelled)
        {
            if (!streamedFinishSegment)
            {
                ChatCompletionChunk finalChunk = runResult.FullResponse.ToFinalChunk(cco.Model, HttpContext.TraceIdentifier);
                await YieldResponse(finalChunk, cancellationToken);
            }

            await Response.Body.WriteAsync("data: [DONE]\n\n"u8.ToArray(), cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        db.UserApiUsages.Add(new UserApiUsage
        {
            ApiKeyId = currentApiKey.ApiKeyId,
            UsageId = runResult.UserModelUsageId,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        if (hasSuccessYield && cco.Streamed)
        {
            return (new EmptyResult(), runResult);
        }
        else if (errorToReturn != null)
        {
            return (errorToReturn, runResult);
        }
        else
        {
            FullChatCompletion fullChatCompletion = runResult.FullResponse.ToOpenAIFullChat(cco.Model, HttpContext.TraceIdentifier);
            return (Content(fullChatCompletion.SerializeForApi(), "application/json"), runResult);
        }
    }

    private readonly static ReadOnlyMemory<byte> dataU8 = "data: "u8.ToArray();
    private readonly static ReadOnlyMemory<byte> lflfU8 = "\n\n"u8.ToArray();

    private BadRequestObjectResult ErrorMessage(DBFinishReason code, string message)
    {
        return BadRequest(new ErrorResponse()
        {
            Error = new ErrorDetail
            {
                Code = code.ToString(),
                Message = message,
                Param = null,
                Type = ""
            }
        });
    }

    private async Task<BadRequestObjectResult> YieldError(bool shouldStreamed, DBFinishReason code, string message, CancellationToken cancellationToken)
    {
        if (shouldStreamed)
        {
            await Response.Body.WriteAsync(dataU8, cancellationToken);
            await JsonSerializer.SerializeAsync(Response.Body, new ErrorResponse()
            {
                Error = new ErrorDetail
                {
                    Code = code.ToString(),
                    Message = message,
                    Param = null,
                    Type = ""
                }
            }, JSON.JsonSerializerOptions, cancellationToken);
            await Response.Body.WriteAsync(lflfU8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        return ErrorMessage(code, message);
    }

    private async Task YieldResponse(ChatCompletionChunk chunk, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(dataU8, cancellationToken);
        await JsonSerializer.SerializeAsync(Response.Body, chunk, JSON.JsonSerializerOptions, cancellationToken);
        await Response.Body.WriteAsync(lflfU8, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task<ContentResult> YieldRawError(bool shouldStreamed, int statusCode, string rawBody, CancellationToken cancellationToken)
    {
        if (shouldStreamed)
        {
            await Response.Body.WriteAsync(dataU8, cancellationToken);
            await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(rawBody), cancellationToken);
            await Response.Body.WriteAsync(lflfU8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        return new ContentResult
        {
            Content = rawBody,
            ContentType = "application/json",
            StatusCode = statusCode
        };
    }

    private BadRequestObjectResult InvalidModel(string? modelName)
    {
        return ErrorMessage(DBFinishReason.InvalidModel, $"The model `{modelName}` does not exist or you do not have access to it.");
    }
}
