using Chats.DB;
using Chats.BE.Controllers.Public.SiteInfos.Dtos;
using Chats.BE.Services.Configs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Public.SiteInfos;

[Route("api/public"), AllowAnonymous]
public class SiteInfoController() : ControllerBase
{
    [HttpGet("siteInfo")]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(SiteInfo), 200)]
    public async Task<ActionResult<SiteInfo>> GetSiteInfo([FromServices] GlobalDBConfig config, CancellationToken cancellationToken)
    {
        SiteInfo? info = await config.GetFillingInfo(cancellationToken);
        if (info == null)
        {
            return Ok(new SiteInfo());
        }

        return Ok(info);
    }

    [HttpGet("login-providers")]
    public async Task<ActionResult<IEnumerable<LoginProviderDto>>> GetLoginProviders([FromServices] ChatsDB db, CancellationToken cancellationToken)
    {
        LoginProviderDto[] providers = await db.LoginServices
            .Where(x => x.Enabled)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(p => new LoginProviderDto
            {
                Id = p.Id,
                Key = p.Type,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(providers);
    }
}
