using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Containers;

/// <summary>
/// Resolves the container quota that applies to a user. Administrators bypass
/// all user quotas (count/cpu/memory/process/volume/image/network/port-mapping/
/// published-site); they remain constrained by runtime node capacity and the
/// backend's safety limits (CodePod MaxResourceLimits, node port pools).
/// </summary>
public static class ContainerQuotaResolver
{
    public const string AdminRole = "admin";

    /// <returns>The effective quota, or <c>null</c> when the user is unrestricted.</returns>
    public static async Task<UserContainerQuotum?> ResolveAsync(ChatsDB db, int userId, CancellationToken cancellationToken)
    {
        if (await IsAdminAsync(db, userId, cancellationToken)) return null;
        return await db.UserContainerQuota.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? await db.UserContainerQuota.FirstOrDefaultAsync(x => x.UserId == null, cancellationToken);
    }

    public static Task<bool> IsAdminAsync(ChatsDB db, int userId, CancellationToken cancellationToken)
        => db.Users.AnyAsync(x => x.Id == userId && x.Role == AdminRole, cancellationToken);
}
