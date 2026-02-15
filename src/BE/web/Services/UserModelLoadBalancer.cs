using Chats.DB;
using System.Collections.Concurrent;

namespace Chats.BE.Services;

public class UserModelLoadBalancer
{
    private static readonly ConcurrentDictionary<string, long> _counters = new();

    public UserModel Select(int userId, string modelName, UserModel[] candidates)
    {
        ArgumentNullException.ThrowIfNull(modelName);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Length == 0)
        {
            throw new ArgumentException("No candidate models available.", nameof(candidates));
        }

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        string key = $"{userId}:{modelName}";
        long next = _counters.AddOrUpdate(key, 0, static (_, current) => current == long.MaxValue ? 0 : current + 1);
        int index = (int)(next % candidates.Length);
        return candidates[index];
    }
}
