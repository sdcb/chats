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
            .Where(x => x.Key == apiKey && !x.IsDeleted && !x.IsRevoked && x.User.Enabled && x.User.ApiKeyEnabled)
            .Select(x => new ApiKeyEntry()
            {
                UserId = x.User.Id,
                UserName = x.User.DisplayName,
                Role = x.User.Role,
                ApiKeyEnabled = true,
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

    public void InvalidateApiKey(string apiKey)
    {
        _cache.Remove(apiKey);
    }

    public void InvalidateUser(int userId)
    {
        string[] keys = _cache
            .Where(x => x.Value is ApiKeyEntry entry && entry.UserId == userId)
            .Select(x => x.Key)
            .ToArray();
        foreach (string key in keys)
        {
            _cache.Remove(key);
        }
    }
}
