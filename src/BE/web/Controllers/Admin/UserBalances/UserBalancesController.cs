using Chats.Web.Controllers.Admin.Common;
using Chats.Web.Controllers.Admin.UserBalances.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Infrastructure;
using Chats.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Controllers.Admin.UserBalances;

[Route("api/admin/user-balances"), AuthorizeAdmin]
public class UserBalancesController(ChatsDB db, CurrentUser currentUser, BalanceService balanceService) : ControllerBase
{
    [HttpPut]
    public async Task<ActionResult<decimal>> ChargeBalance([FromBody] ChargeBalanceRequest request, CancellationToken cancellationToken)
    {
        db.BalanceTransactions.Add(new BalanceTransaction()
        {
            UserId = request.UserId,
            Amount = request.Amount,
            TransactionTypeId = (byte)DBTransactionType.Charge,
            CreatedAt = DateTime.UtcNow,
            CreditUserId = currentUser.Id,
        });
        await db.SaveChangesAsync(cancellationToken);
        await balanceService.UpdateBalance(db, request.UserId, cancellationToken);

        return await db.UserBalances
            .Where(x => x.UserId == request.UserId)
            .Select(x => x.Balance)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

