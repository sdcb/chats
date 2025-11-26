using Chats.BE.Controllers.Api.AnthropicCompatible.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace Chats.BE.Controllers.Api.AnthropicCompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public class AnthropicCountTokenController(
    CurrentApiKey currentApiKey,
    ChatFactory cf,
    UserModelManager userModelManager,
    ILogger<AnthropicCountTokenController> logger) : ControllerBase
{
    private static readonly DBApiType[] AllowedApiTypes = [DBApiType.OpenAIChatCompletion, DBApiType.OpenAIResponse, DBApiType.AnthropicMessages];

    [HttpPost("v1/messages/count_tokens")]
    public async Task<ActionResult> CountTokens([FromBody] JsonObject json, CancellationToken cancellationToken)
    {
        AnthropicCountTokenRequestWrapper request = new(json);

        if (!request.SeemsValid())
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, "Invalid request: model and messages are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, "model is required.");
        }

        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, request.Model, cancellationToken);
        if (userModel == null)
        {
            return ErrorMessage(AnthropicErrorTypes.NotFoundError, $"The model `{request.Model}` does not exist or you do not have access to it.");
        }

        if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
        {
            return ErrorMessage(AnthropicErrorTypes.InvalidRequestError, $"The model `{request.Model}` does not support messages API.");
        }

        try
        {
            Model cm = userModel.Model;
            using ChatService s = cf.CreateChatService(cm);
            ChatRequest chatRequest = request.ToChatRequest(currentApiKey.User.Id.ToString(), cm);
            int inputTokens = await s.CountTokenAsync(chatRequest, cancellationToken);

            return Ok(new AnthropicCountTokenResponse { InputTokens = inputTokens });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting tokens");
            return ErrorMessage(AnthropicErrorTypes.ApiError, "Internal server error");
        }
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
}

public class AnthropicCountTokenRequestWrapper(JsonObject json)
{
    public string? Model => (string?)json["model"];

    public bool SeemsValid()
    {
        return Model != null && json["messages"] != null;
    }

    public ChatRequest ToChatRequest(string userId, Model model)
    {
        // For count tokens, we reuse the AnthropicRequestWrapper logic
        // but we need to ensure max_tokens has a default value
        json["max_tokens"] ??= model.MaxResponseTokens;
        json["stream"] ??= false;

        AnthropicRequestWrapper wrapper = new(json);
        return wrapper.ToChatRequest(userId, model);
    }
}
