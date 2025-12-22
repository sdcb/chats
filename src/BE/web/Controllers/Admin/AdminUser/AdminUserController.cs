using Chats.Web.Controllers.Admin.AdminUser.Dtos;
using Chats.Web.Controllers.Admin.Common;
using Chats.Web.Controllers.Common.Dtos;
using Chats.Web.DB;
using Chats.Web.Infrastructure;
using Chats.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Controllers.Admin.AdminUser;

[Route("api/admin/users"), AuthorizeAdmin]
public class AdminUserController(ChatsDB db, CurrentUser adminUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(QueryPagingRequest pagingRequest, CancellationToken cancellationToken)
    {
        IQueryable<User> query = db.Users
            .OrderByDescending(x => x.UpdatedAt);
        if (!string.IsNullOrEmpty(pagingRequest.Query))
        {
            query = query.Where(x => x.UserName == pagingRequest.Query);
        }

        return await PagedResult.FromTempQuery(query.Select(x => new AdminUserDtoTemp()
        {
            Id = x.Id,
            Username = x.DisplayName,
            Account = x.UserName,
            Balance = x.UserBalance!.Balance.ToString(),
            Role = x.Role,
            Avatar = x.Avatar,
            Phone = x.Phone,
            Email = x.Email,
            Provider = x.Provider,
            Enabled = x.Enabled,
            CreatedAt = x.CreatedAt,
            UserModelCount = x.UserModels.Count(),
        }), pagingRequest, x => x.ToDto(), cancellationToken);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto dto, [FromServices] PasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        User? user = await db.Users.FindAsync([dto.UserId], cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        dto.ApplyToUser(user, passwordHasher);
        if (db.ChangeTracker.HasChanges())
        {
            user.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto, [FromServices] PasswordHasher passwordHasher, [FromServices] UserManager userManager, CancellationToken cancellationToken)
    {
        User? existingUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == dto.UserName, cancellationToken);
        if (existingUser != null)
        {
            return BadRequest("User existed");
        }

        User user = new()
        {
            UserName = dto.UserName,
            DisplayName = dto.UserName,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = dto.Role,
            Avatar = dto.Avatar,
            Enabled = dto.Enabled ?? false,
            Provider = null,
            PasswordHash = passwordHasher.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await userManager.InitializeUserWithoutSave(user, null, null, adminUser.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Created(default(string), value: user.Id);
    }
}
