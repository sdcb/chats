using Chats.Web.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Services;

public class BalanceService(IServiceScopeFactory serviceScopeFactory)
{
    public Task AsyncUpdateBalance(int userId, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            await UpdateBalance(db, userId, cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateBalance(ChatsDB db, int userId, CancellationToken cancellationToken)
    {
        await db.UserBalances
            .Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(userBalance => userBalance.SetProperty(p => p.Balance, v =>
                db.BalanceTransactions
                    .Where(x => x.UserId == userId)
                    .Sum(x => x.Amount)
                ), cancellationToken);
    }

    public Task AsyncUpdateUsage(IEnumerable<int> userModelIds, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            foreach (int userModelId in userModelIds)
            {
                await UpdateUsage(db, userModelId, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task UpdateUsage(ChatsDB db, int userModelId, CancellationToken cancellationToken)
    {
        await db.UserModels
            .Where(x => x.Id == userModelId)
            .ExecuteUpdateAsync(userModel => userModel
                .SetProperty(p => p.TokenBalance, v => db.UsageTransactions.Where(x => x.ModelId == v.ModelId && x.CreditUserId == v.UserId).Sum(x => x.TokenAmount))
                .SetProperty(p => p.CountBalance, v => db.UsageTransactions.Where(x => x.ModelId == v.ModelId && x.CreditUserId == v.UserId).Sum(x => x.CountAmount)), cancellationToken);
    }
}