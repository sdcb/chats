using Chats.DB;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.LoginServices.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.LoginServices;

[Route("api/admin/login-service"), AuthorizeAdmin]
public class LoginServiceController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public LoginServiceDto[] GetLoginServices()
    {
        LoginServiceDto[] result = db.LoginServices
            .Select(x => new LoginServiceDtoTemp()
            {
                Id = x.Id,
                Type = x.Type,
                Configs = x.Configs,
                Enabled = x.Enabled,
                CreatedAt = x.CreatedAt
            })
            .AsEnumerable()
            .Select(x => x.ToDto())
            .ToArray();
        return result;
    }

    [HttpPut("{loginServiceId:int}")]
    public async Task<ActionResult> UpdateLoginService(int loginServiceId, [FromBody] LoginServiceUpdateRequest dto, CancellationToken cancellationToken)
    {
        LoginService? entity = await db.LoginServices.FindAsync([loginServiceId], cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        dto.ApplyTo(entity);
        if (db.ChangeTracker.HasChanges())
        {
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> CreateLoginService([FromBody] LoginServiceUpdateRequest dto, CancellationToken cancellationToken)
    {
        // duplicated type is not allowed
        if (await db.LoginServices.AnyAsync(x => x.Type == dto.Type && x.Enabled, cancellationToken))
        {
            return BadRequest("Duplicated type is not allowed");
        }

        LoginService entity = new()
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dto.ApplyTo(entity);
        db.LoginServices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetLoginServices), new { id = entity.Id }, entity.Id);
    }
}
