using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using System.Security.Claims;

namespace Chats.BE.Infrastructure;

public class CurrentUser
{
    public CurrentUser(IHttpContextAccessor httpContextAccessor, IUrlEncryptionService idEncryption)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is null");

        Id = idEncryption.DecryptUserIdOrNull(httpContext.User.FindFirstValue(JwtPropertyKeys.UserId)) ?? throw new InvalidOperationException("Failed to decrypt UserId.");
        UserName = httpContext.User.FindFirstValue(JwtPropertyKeys.UserName) ?? throw new InvalidOperationException("User name is null");
        Role = httpContext.User.FindFirstValue(JwtPropertyKeys.Role) ?? throw new InvalidOperationException("User role is null");
    }

    public int Id { get; }
    public string UserName { get; }
    public string Role { get; }

    public bool IsAdmin => Role == "admin";
}