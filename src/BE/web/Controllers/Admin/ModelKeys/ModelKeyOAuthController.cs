using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Services.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.ModelKeys;

[Route("api/admin/model-keys"), AuthorizeAdmin]
public class ModelKeyOAuthController(OpenAIModelOAuthService oauthService) : ControllerBase
{
    [HttpPost("{modelKeyId:int}/oauth/openai/start")]
    public async Task<ActionResult<OpenAIModelOAuthStartResult>> StartOpenAIOAuth(short modelKeyId, [FromQuery] bool allowApiKeySource = false, CancellationToken cancellationToken = default)
    {
        try
        {
            string callbackUrl = "http://localhost:1455/auth/callback";
            OpenAIModelOAuthStartResult result = await oauthService.StartAuthorizationAsync(modelKeyId, callbackUrl, cancellationToken, allowApiKeySource);
            return Ok(result);
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description,
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("oauth/openai/callback")]
    public async Task<IActionResult> OpenAIOAuthCallback([FromQuery] string? state, [FromQuery] string? code, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        return await HandleOpenAIOAuthCallback(state, code, error, errorDescription, cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("/auth/callback")]
    public async Task<IActionResult> OpenAIOAuthCallbackCompat([FromQuery] string? state, [FromQuery] string? code, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        return await HandleOpenAIOAuthCallback(state, code, error, errorDescription, cancellationToken);
    }

    private async Task<IActionResult> HandleOpenAIOAuthCallback(string? state, string? code, string? error, string? errorDescription, CancellationToken cancellationToken)
    {
        try
        {
            OpenAIModelOAuthCallbackResult result = await oauthService.HandleCallbackAsync(state, code, error, errorDescription, cancellationToken);
            if (result.Success)
            {
                return BuildCallbackPage(
                    success: true,
                    modelKeyId: result.ModelKeyId,
                    message: $"OpenAI OAuth connected for model key {result.ModelKeyId}. You can close this page.");
            }

            return BuildCallbackPage(
                success: false,
                modelKeyId: result.ModelKeyId,
                message: $"OpenAI OAuth failed: {result.Error}. {result.ErrorDescription}",
                error: result.Error,
                errorDescription: result.ErrorDescription);
        }
        catch (OAuthProtocolException ex)
        {
            return BuildCallbackPage(
                success: false,
                modelKeyId: null,
                message: $"OpenAI OAuth failed: {ex.Error}. {ex.Description}",
                error: ex.Error,
                errorDescription: ex.Description,
                statusCode: ex.StatusCode);
        }
    }

    private static IActionResult BuildCallbackPage(bool success, short? modelKeyId, string message, string? error = null, string? errorDescription = null, int statusCode = 200)
    {
        object payload = new
        {
            type = "openai-modelkey-oauth-callback",
            success,
            modelKeyId,
            error,
            errorDescription,
            message,
        };
        string payloadJson = JsonSerializer.Serialize(payload);
        string title = success ? "OpenAI OAuth Connected" : "OpenAI OAuth Failed";
        string actionTip = success ? "Authorization completed. You can close this page." : "Authorization failed. Please close this page and retry from admin UI.";

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{HtmlEncoder.Default.Encode(title)}}</title>
</head>
<body style="font-family:Segoe UI,Arial,sans-serif;margin:24px;line-height:1.6;">
  <h2>{{HtmlEncoder.Default.Encode(title)}}</h2>
  <p>{{HtmlEncoder.Default.Encode(message)}}</p>
  <p>{{HtmlEncoder.Default.Encode(actionTip)}}</p>
  <button onclick="window.close()" style="padding:6px 12px;cursor:pointer;">Close</button>
  <script>
    const payload = {{payloadJson}};
    try {
      if (window.opener && !window.opener.closed) {
        window.opener.postMessage(payload, "*");
      }
    } catch {}
  </script>
</body>
</html>
""";

        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = "text/html; charset=utf-8",
            Content = html,
        };
    }

    [HttpGet("{modelKeyId:int}/oauth/openai/status")]
    public async Task<ActionResult<OpenAIModelOAuthStatusResult>> OpenAIOAuthStatus(short modelKeyId, CancellationToken cancellationToken)
    {
        try
        {
            OpenAIModelOAuthStatusResult status = await oauthService.GetStatusAsync(modelKeyId, cancellationToken);
            return Ok(status);
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description,
            });
        }
    }

    [HttpPost("{modelKeyId:int}/oauth/openai/disconnect")]
    public async Task<IActionResult> DisconnectOpenAIOAuth(short modelKeyId, CancellationToken cancellationToken)
    {
        try
        {
            await oauthService.DisconnectAsync(modelKeyId, cancellationToken);
            return NoContent();
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description,
            });
        }
    }
}
