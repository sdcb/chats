using Chats.DB;
using Chats.BE.Controllers.Users.ChangePassword.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Users.ChangePassword;

[Route("api/user"), Authorize]
public class ChangePasswordController(ChatsDB db, CurrentUser currentUser, PasswordHasher passwordHasher) : ControllerBase
{
    [HttpPut("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken cancellationToken)
    {
        if (req.NewPassword != req.ConfirmPassword)
        {
            return BadRequest("New password and confirm password do not match");
        }

        if (!PasswordPolicy.IsStrongEnough(req.NewPassword))
        {
            return BadRequest(PasswordPolicy.ErrorMessage);
        }

        User? user = await db.Users
            .Where(x => x.Id == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        // only check old password when it is set
        if (user.PasswordHash != null)
        {
            if (!passwordHasher.VerifyPassword(req.OldPassword, user.PasswordHash))
            {
                return BadRequest("Old password incorrect");
            }

            if (passwordHasher.VerifyPassword(req.NewPassword, user.PasswordHash))
            {
                return BadRequest("New password should be different from the old one");
            }
        }

        user.PasswordHash = passwordHasher.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

}
