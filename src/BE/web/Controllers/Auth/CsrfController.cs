using Chats.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chats.Web.Controllers.Auth;

[Route("api/auth/csrf")]
public class CsrfController(CsrfTokenService csrf) : ControllerBase
{
    [HttpGet]
    public IActionResult GetToken()
    {
        string token = csrf.GenerateToken();
        return Ok(new { CsrfToken = token });
    }
}
