using Chats.BE.Infrastructure;
using System.Security.Claims;

namespace Chats.BE.Services.OpenAIApiKeySession;

/// <summary>
/// Note: This class is only used for OpenAIApiKey and OAuthAccessToken authentication schemes.
/// </summary>
public class CurrentApiKey
{
    public CurrentApiKey(CurrentUser currentUser, IHttpContextAccessor httpContextAccessor)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is null");
        ClaimsPrincipal user = httpContext.User;

        User = currentUser;
        ApiKeyId = int.Parse(user.FindFirstValue("api-key-id") ?? throw new InvalidOperationException("API Key id is null"));
        ApiKey = user.FindFirstValue("api-key") ?? string.Empty;
    }

    public CurrentUser User { get; }

    public string ApiKey { get; }

    public int ApiKeyId { get; }
}
