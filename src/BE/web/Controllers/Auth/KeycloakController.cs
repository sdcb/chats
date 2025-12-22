using Chats.Web.Controllers.Auth.Dtos;
using Chats.Web.DB.Jsons;
using Chats.Web.Services;
using Chats.Web.Services.Configs;
using Microsoft.AspNetCore.Mvc;

namespace Chats.Web.Controllers.Auth;

[Route("api/auth/signin/keycloak")]
public class KeycloakController(CsrfTokenService csrf, GlobalDBConfig globalConfig) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SignIn(KeycloakSigninRequest req, CancellationToken cancellationToken)
    {
        if (!csrf.VerifyToken(req.CsrfToken))
        {
            return BadRequest("Invalid CSRF token");
        }

        JsonKeycloakConfig? config = await globalConfig.GetKeycloakConfig(cancellationToken);
        if (config == null)
        {
            return NotFound("Keycloak config not found");
        }

        string keycloakRedirectUrl = await config.GenerateLoginUrl(req.CallbackUrl, cancellationToken);
        return Redirect(keycloakRedirectUrl);
    }
}
