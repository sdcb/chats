using Chats.DB;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Caching;

namespace Chats.BE.Services.OpenAIApiKeySession;

public class OpenAIApiKeySessionManager(ChatsDB db)
{
    private static readonly MemoryCache _cache = new("OpenAIApiKeySessionManager");
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<ApiKeyEntry?> GetUserInfoByOpenAIApiKey(string apiKey, CancellationToken cancellationToken = default)
    {
        ApiKeyEntry? sessionEntry = await db.UserApiKeys
            .Include(x => x.User)
            .Where(x => x.Key == apiKey && !x.IsDeleted)
            .Select(x => new ApiKeyEntry()
            {
                UserId = x.User.Id,
                UserName = x.User.DisplayName,
                Role = x.User.Role,
                ApiKey = apiKey,
                ApiKeyId = x.Id,
                Expires = x.Expires
            })
            .FirstOrDefaultAsync(cancellationToken);
        return sessionEntry;
    }

    public async Task<ApiKeyEntry?> GetCachedUserInfoByOpenAIApiKey(string apiKey, CancellationToken cancellationToken = default)
    {
        if (_cache.Get(apiKey) is ApiKeyEntry cachedEntry)
        {
            return cachedEntry;
        }

        ApiKeyEntry? sessionEntry = await GetUserInfoByOpenAIApiKey(apiKey, cancellationToken);
        if (sessionEntry != null)
        {
            _cache.Set(apiKey, sessionEntry, new CacheItemPolicy()
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(_cacheDuration)
            });
        }
        return sessionEntry;
    }
}
