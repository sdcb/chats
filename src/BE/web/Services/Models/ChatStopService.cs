using Microsoft.AspNetCore.WebUtilities;
using System.Runtime.Caching;

namespace Chats.Web.Services.Models;

public class ChatStopService
{
    private readonly MemoryCache _cache = new(nameof(ChatStopService));
    private readonly CacheItemPolicy _cacheItemPolicy = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
    };

    public string CreateAndCombineCancellationToken(ref CancellationToken cancellationToken)
    {
        CancellationTokenSource cts = new();
        string key = WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
        cancellationToken = linkedCts.Token;

        if (!_cache.Add(key, (cts, linkedCts), _cacheItemPolicy))
        {
            throw new InvalidOperationException("Failed to add stop token to cache");
        }

        return key;
    }

    public bool TryCancel(string key)
    {
        if (_cache.Get(key) is not (CancellationTokenSource cts, CancellationTokenSource _))
        {
            return false;
        }

        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    public bool Remove(string key)
    {
        if (_cache.Remove(key) is not (CancellationTokenSource cts, CancellationTokenSource linkedCts))
        {
            return false;
        }

        cts.Dispose();
        linkedCts.Dispose();
        return true;
    }
}