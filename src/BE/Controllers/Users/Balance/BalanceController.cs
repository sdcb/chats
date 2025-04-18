using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Users.Balance;

[Route("api/user"), Authorize]
public class BalanceController(ChatsDB db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet("balance-only")]
    public async Task<ActionResult<decimal>> GetBalanceOnly(CancellationToken cancellationToken)
    {
        decimal balance = await db.Users
            .Where(x => x.Id == currentUser.Id)
            .Select(x => x.UserBalance!.Balance)
            .FirstOrDefaultAsync(cancellationToken);
        return Ok(balance);
    }
}
