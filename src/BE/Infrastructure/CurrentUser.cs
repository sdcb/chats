using Chats.BE.Services.Sessions;
using System.Security.Claims;

namespace Chats.BE.Infrastructure;

public class CurrentUser
{
    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is null");

        Id = int.TryParse(httpContext.User.FindFirstValue(JwtPropertyKeys.UserId), out int id) ? id : throw new InvalidOperationException("User id is not a int");
        DisplayName = httpContext.User.FindFirstValue(JwtPropertyKeys.UserName) ?? throw new InvalidOperationException("User name is null");
        Role = httpContext.User.FindFirstValue(JwtPropertyKeys.Role) ?? throw new InvalidOperationException("User role is null");
        Provider = httpContext.User.FindFirstValue(JwtPropertyKeys.Provider);
        ProviderSub = httpContext.User.FindFirstValue(JwtPropertyKeys.ProviderSub);
    }

    public int Id { get; }
    public string DisplayName { get; }
    public string Role { get; }
    public string? Provider { get; }
    public string? ProviderSub { get; }

    public bool IsAdmin => Role == "admin";
}