using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ClientModel;
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

namespace Chats.BE.Controllers.Api.OpenAICompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public partial class OpenAIChatCompletionController(
    ChatsDB db,
    CurrentApiKey currentApiKey,
    ChatFactory cf,
    UserModelManager userModelManager,
    ILogger<OpenAIChatCompletionController> logger,
    BalanceService balanceService,
    FileUrlProvider fup,
    AsyncCacheUsageManager asyncCacheUsageService) : ControllerBase
{
    private static readonly DBApiType[] AllowedApiTypes = [DBApiType.OpenAIChatCompletion, DBApiType.OpenAIResponse, DBApiType.AnthropicMessages];

    [HttpPost("v1/chat/completions")]
    public async Task<ActionResult> ChatCompletion([FromBody] JsonObject json, [FromServices] AsyncClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        InChatContext icc = new(Stopwatch.GetTimestamp());
        CcoWrapper cco = new(json);
        if (!cco.SeemsValid())
        {
            return ErrorMessage(DBFinishReason.BadParameter, "bad parameter.");
        }
        if (string.IsNullOrWhiteSpace(cco.Model))
        {
            return InvalidModel(cco.Model);
        }

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, cco.Model, cancellationToken);
        if (userModel == null) return InvalidModel(cco.Model);

        if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
        {
            return ErrorMessage(DBFinishReason.BadParameter, $"The model `{cco.Model}` does not support chat completions API.");
        }

        CcoCacheControl? ccoCacheControl = cco.CacheControl;
        if (ccoCacheControl != null)
        {
            cco.CacheControl = null;
            return await ChatCompletionUseCache(ccoCacheControl, cco, userModel, icc, clientInfoIdTask, cancellationToken);
        }

        return await ChatCompletionNoCache(cco, userModel, icc, clientInfoIdTask, cancellationToken);
    }

    [HttpPost("v1-cached/chat/completions")]
    [HttpPost("v1-cached-createOnly/chat/completions")]
    public async Task<ActionResult> ChatCompletionCached([FromBody] JsonObject json, [FromServices] AsyncClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        InChatContext icc = new(Stopwatch.GetTimestamp());
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], Started", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds);
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

            Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
            UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, cco.Model, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], GetUserModel", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds);
            }
            if (userModel == null) return InvalidModel(cco.Model);

            if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
            {
                return ErrorMessage(DBFinishReason.BadParameter, $"The model `{cco.Model}` does not support chat completions API.");
            }

            CcoCacheControl ccoCacheControl = cco.CacheControl ?? CcoCacheControl.StaticCached with
            {
                CreateOnly = Request.GetDisplayUrl().Contains("v1-cached-createOnly", StringComparison.OrdinalIgnoreCase),
            };
            cco.CacheControl = null;
            return await ChatCompletionUseCache(ccoCacheControl, cco, userModel, icc, clientInfoIdTask, cancellationToken);
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{RequestId} [{Elapsed}], Finish Reason: {FinishReason}", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds, icc.FinishReason.ToString());
            }
        }
    }

    private async Task<ActionResult> ChatCompletionUseCache(
        CcoCacheControl cacheControl,
        CcoWrapper cco,
        UserModel userModel,
        InChatContext icc,
        Task<int> clientInfoIdTask,
        CancellationToken cancellationToken)
    {
        string requestBody = cco.Serialize();
        long requestHashCode = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
        logger.LogInformation("{RequestId} [{Elapsed}], Check Cache", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds);

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
                logger.LogInformation("{RequestId} [{Elapsed}], Cache Found: {CacheFound}", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds, cache != null);
            }

            if (cache != null)
            {
                bool isSuccess = false;
                try
                {
                    FullChatCompletion fullResponse = JsonSerializer.Deserialize<FullChatCompletion>(cache.UserApiCacheBody!.Response)!;
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("{RequestId} [{Elapsed}], Cache Deserialized", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds);
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
                        return Empty;
                    }
                    else
                    {
                        isSuccess = true;
                        return Content(fullResponse.SerializeForApi(), "application/json");
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
                        logger.LogInformation("{RequestId} [{Elapsed}], Response completed", HttpContext.TraceIdentifier, icc.ElapsedTime.TotalMilliseconds);
                    }
                    if (isSuccess)
                    {
                        _ = asyncCacheUsageService.SaveCacheUsage(new UserApiCacheUsage()
                        {
                            ClientInfoId = await clientInfoIdTask,
                            UsedAt = DateTime.UtcNow,
                            UserApiCacheId = cache.Id,
                        }, default);
                    }
                }
            }
        }

        try
        {
            ActionResult result = await ChatCompletionNoCache(cco, userModel, icc, clientInfoIdTask, cancellationToken);
            return result;
        }
        finally
        {
            if (icc.FinishReason == DBFinishReason.Success || icc.FinishReason == DBFinishReason.Stop || icc.FinishReason == DBFinishReason.ToolCalls)
            {
                FullChatCompletion toBeCached = icc.FullResponse!.ToOpenAIFullChat(cco.Model, HttpContext.TraceIdentifier);
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
                    ClientInfoId = await clientInfoIdTask,
                    ModelId = userModel.ModelId,
                };
                db.UserApiCaches.Add(cache);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<ActionResult> ChatCompletionNoCache(CcoWrapper cco, UserModel userModel, InChatContext icc, Task<int> clientInfoIdTask, CancellationToken cancellationToken)
    {
        Model cm = userModel.Model;
        ChatService s = cf.CreateChatService(cm);
        UserBalance userBalance = await db.UserBalances
            .Where(x => x.UserId == currentApiKey.User.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("User balance not found.");
        UserModelBalanceCalculator calc = new(BalanceInitialInfo.FromDB([userModel], userBalance.Balance), []);
        ScopedBalanceCalculator scopedCalc = calc.WithScoped("0");
        BadRequestObjectResult? errorToReturn = null;
        bool hasSuccessYield = false;
        bool streamedFinishSegment = false;
        try
        {
            ChatRequest csr = ChatRequest.FromOpenAI(currentApiKey.User.Id.ToString(), cm, cco.Streamed, cco.Messages!, cco.ToCleanCco());
            await foreach (ChatSegment segment in icc.Run(scopedCalc, userModel, s, csr, fup, cancellationToken))
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

                    ChatCompletionChunk chunk = segment.ToOpenAIChatCompletionChunk(cco.Model, HttpContext.TraceIdentifier, null);
                    await YieldResponse(chunk, cancellationToken);
                    hasSuccessYield = true;
                    streamedFinishSegment |= segment is FinishReasonChatSegment;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
            }

            // 流式响应完成后，发送最终的 finish_reason chunk
            if (cco.Streamed && hasSuccessYield && icc.FinishReason != DBFinishReason.Cancelled)
            {
                if (!streamedFinishSegment)
                {
                    ChatCompletionChunk finalChunk = icc.FullResponse!.ToFinalChunk(cco.Model, HttpContext.TraceIdentifier);
                    await YieldResponse(finalChunk, cancellationToken);
                }

                // 发送 [DONE] 标记
                await Response.Body.WriteAsync("data: [DONE]\n\n"u8.ToArray(), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (ChatServiceException cse)
        {
            icc.FinishReason = cse.ErrorCode;
            errorToReturn = await YieldError(hasSuccessYield && cco.Streamed, cse.ErrorCode, cse.Message, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            icc.FinishReason = DBFinishReason.Cancelled;
        }
        catch (Exception e)
        {
            icc.FinishReason = DBFinishReason.UnknownError;
            logger.LogError(e, "Unknown error");
            errorToReturn = await YieldError(hasSuccessYield && cco.Streamed, icc.FinishReason, "", cancellationToken);
        }
        finally
        {
            // disable the cancellationToken because following code is credit deduction related
            cancellationToken = CancellationToken.None;
        }

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

        if (hasSuccessYield && cco.Streamed)
        {
            return new EmptyResult();
        }
        else if (errorToReturn != null)
        {
            return errorToReturn;
        }
        else
        {
            // non-streamed success
            FullChatCompletion fullChatCompletion = icc.FullResponse!.ToOpenAIFullChat(cco.Model, HttpContext.TraceIdentifier);
            return Content(fullChatCompletion.SerializeForApi(), "application/json");
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

    private BadRequestObjectResult InvalidModel(string? modelName)
    {
        return ErrorMessage(DBFinishReason.InvalidModel, $"The model `{modelName}` does not exist or you do not have access to it.");
    }
}
