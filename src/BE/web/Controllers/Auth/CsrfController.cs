using Chats.BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Auth;

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
