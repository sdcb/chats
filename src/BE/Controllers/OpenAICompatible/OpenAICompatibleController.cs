﻿using Chats.BE.Controllers.Chats.Conversations;
using Chats.BE.DB;
using Chats.BE.Services;
using Chats.BE.Services.Conversations;
using Chats.BE.Services.Conversations.Dtos;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sdcb.DashScope;
using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.OpenAICompatible;

[Route("v1"), Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public partial class OpenAICompatibleController(ChatsDB db, CurrentApiKey currentApiKey, ConversationFactory cf, UserModelManager userModelManager, ILogger<OpenAICompatibleController> logger, BalanceService balanceService) : ControllerBase
{
    [HttpPost("chat/completions")]
    public async Task<ActionResult> ChatCompletion([FromBody] JsonObject json, [FromServices] ClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        InChatContext icc = new();
        CcoWrapper cco = new(json);
        if (!cco.SeemsValid())
        {
            return ErrorMessage(OpenAICompatibleErrorCode.BadParameter, "bad parameter.");
        }
        if (string.IsNullOrWhiteSpace(cco.Model)) return InvalidModel(cco.Model);

        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, cco.Model, cancellationToken);
        if (userModel == null) return InvalidModel(cco.Model);

        Model cm = userModel.Model;
        using ConversationService s = cf.CreateConversationService(cm);

        UserBalance userBalance = await db.UserBalances
            .Where(x => x.UserId == currentApiKey.User.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("User balance not found.");
        BadRequestObjectResult? errorToReturn = null;
        bool hasSuccessYield = false;
        try
        {
            await foreach (InternalChatSegment seg in icc.Run(userBalance.Balance, userModel, s.ChatStreamedSimulated([.. cco.Messages], cco.ToCleanCco(), cancellationToken)))
            {
                if (seg.TextSegment == string.Empty) continue;

                if (cco.Stream)
                {
                    ChatCompletionChunk chunk = seg.ToOpenAIChunk(cco.Model, HttpContext.TraceIdentifier);
                    await YieldResponse(chunk, cancellationToken);
                    hasSuccessYield = true;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (ChatServiceException cse)
        {
            errorToReturn = await YieldError(hasSuccessYield && cco.Stream, cse.ErrorCode, cse.Message, cancellationToken);
        }
        catch (Exception e) when (e is DashScopeException || e is ClientResultException)
        {
            logger.LogError(e, "Upstream error");
            errorToReturn = await YieldError(hasSuccessYield && cco.Stream, OpenAICompatibleErrorCode.UpstreamError, e.Message, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // do nothing if cancelled
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unknown error");
            errorToReturn = await YieldError(hasSuccessYield && cco.Stream, OpenAICompatibleErrorCode.Unknown, "\n⚠Error in conversation - 对话出错!", cancellationToken);
        }
        finally
        {
            // disable the cancellationToken because following code is credit deduction related
            cancellationToken = CancellationToken.None;
        }

        UserApiUsage usage = new()
        {
            ApiKeyId = currentApiKey.ApiKeyId,
            Usage = icc.ToUserModelUsage(currentApiKey.User.Id, await clientInfoManager.GetClientInfo(cancellationToken), true),
        };
        db.UserApiUsages.Add(usage);
        await db.SaveChangesAsync(cancellationToken);
        if (icc.Cost.CostBalance > 0)
        {
            _ = balanceService.AsyncUpdateBalance(currentApiKey.User.Id, CancellationToken.None);
        }
        if (icc.Cost.CostUsage)
        {
            _ = balanceService.AsyncUpdateUsage([userModel!.Id], CancellationToken.None);
        }

        if (hasSuccessYield && cco.Stream)
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
            FullChatCompletion fullChatCompletion = icc.FullResponse.ToOpenAIFullChat(cco.Model, HttpContext.TraceIdentifier);
            return Ok(fullChatCompletion);
        }
    }

    private readonly static ReadOnlyMemory<byte> dataU8 = "data: "u8.ToArray();
    private readonly static ReadOnlyMemory<byte> lflfU8 = "\n\n"u8.ToArray();

    private BadRequestObjectResult ErrorMessage(OpenAICompatibleErrorCode code, string message)
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

    private async Task<BadRequestObjectResult> YieldError(bool shouldStreamed, OpenAICompatibleErrorCode code, string message, CancellationToken cancellationToken)
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
        return ErrorMessage(OpenAICompatibleErrorCode.InvalidModel, $"The model `{modelName}` does not exist or you do not have access to it.");
    }

    [HttpGet("models")]
    public async Task<ActionResult<ModelListDto>> GetModels(CancellationToken cancellationToken)
    {
        UserModel[] models = await userModelManager.GetValidModelsByApiKey(currentApiKey.ApiKey, cancellationToken);
        return Ok(new ModelListDto
        {
            Object = "list",
            Data = models.Select(x => new ModelListItemDto
            {
                Id = x.Model.Name,
                Created = new DateTimeOffset(x.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds(),
                Object = "model",
                OwnedBy = x.Model.ModelKey.Name
            }).ToArray()
        });
    }
}