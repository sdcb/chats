namespace Chats.Web.Controllers.Auth.Dtos;

public record KeycloakSigninRequest
{
    public required string CsrfToken { get; init; }

    public required string CallbackUrl { get; init; }
}
