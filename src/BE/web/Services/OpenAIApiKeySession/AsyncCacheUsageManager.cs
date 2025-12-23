using Chats.DB;

namespace Chats.BE.Services.OpenAIApiKeySession;

public class AsyncCacheUsageManager(IServiceScopeFactory serviceScopeFactory)
{
    public async Task<int> SaveCacheUsage(UserApiCacheUsage usage, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        db.UserApiCacheUsages.Add(usage);
        return await db.SaveChangesAsync(cancellationToken);
    }
}