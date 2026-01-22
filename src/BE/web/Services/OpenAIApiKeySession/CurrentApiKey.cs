using Chats.BE.Infrastructure;
using System.Security.Claims;

namespace Chats.BE.Services.OpenAIApiKeySession;

/// <summary>
/// Note: This class is only used for OpenAIApiKey authentication scheme.
/// </summary>
public class CurrentApiKey(CurrentUser currentUser, IHttpContextAccessor httpContextAccessor)
{
    public CurrentUser User { get; } = currentUser;

    public string ApiKey { get; } = httpContextAccessor.HttpContext!.User.FindFirstValue("api-key") ?? throw new InvalidOperationException("API Key is null");

    public int ApiKeyId { get; } = int.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue("api-key-id") ?? throw new InvalidOperationException("API Key is null"));
}
