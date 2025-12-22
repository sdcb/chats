namespace Chats.Web.Controllers.Public.AccountLogin.Dtos;

public record PasswordLoginRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
}
