using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class UserModelManager(ChatsDB db, UserModelLoadBalancer userModelLoadBalancer)
{
    public async Task<UserModel?> GetUserModel(int userId, short modelId, CancellationToken cancellationToken)
    {
        UserModel? balances = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Where(x => x.UserId == userId && !x.Model.IsDeleted && x.ModelId == modelId)
            .FirstOrDefaultAsync(cancellationToken);

        return balances;
    }

    public async Task<Dictionary<short, UserModel>> GetUserModels(int userId, HashSet<short> modelIds, CancellationToken cancellationToken)
    {
        Dictionary<short, UserModel> balances = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Where(x => x.UserId == userId && !x.Model.IsDeleted && modelIds.Contains(x.ModelId))
            .ToDictionaryAsync(k => k.ModelId, v => v, cancellationToken);

        return balances;
    }

    private async Task<UserModel[]> GetUserModels(int userId, string modelName, CancellationToken cancellationToken)
    {
        UserModel[] userModels = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Where(x => x.UserId == userId && !x.Model.IsDeleted && x.Model.Name == modelName)
            .OrderBy(x => x.ModelId)
            .ToArrayAsync(cancellationToken);

        return userModels;
    }

    public async Task<UserModel?> GetUserModel(string apiKey, string modelName, CancellationToken cancellationToken)
    {
        UserApiKey? key = await db.UserApiKeys
            .Include(x => x.Models)
            .Where(x => x.Key == apiKey && x.Expires > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
        if (key == null) return null;

        UserModel[] userModels = await GetUserModels(key.UserId, modelName, cancellationToken);
        if (userModels.Length == 0)
        {
            return null;
        }

        if (!key.AllowAllModels)
        {
            HashSet<short> selectedModels = key.Models.Select(x => x.Id).ToHashSet();
            userModels = userModels.Where(x => selectedModels.Contains(x.ModelId)).ToArray();
            if (userModels.Length == 0)
            {
                return null;
            }
        }

        return userModelLoadBalancer.Select(key.UserId, modelName, userModels);
    }

    public IOrderedQueryable<UserModel> GetValidModelsByUserId(int userId)
    {
        return db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Where(x => x.UserId == userId && !x.Model.IsDeleted)
            .OrderBy(x => x.Model.Order);
    }

    public async Task<UserModel[]> GetValidModelsByApiKey(string apiKey, CancellationToken cancellationToken)
    {
        UserApiKey? key = await db.UserApiKeys
            .Include(x => x.Models)
            .Where(x => x.Key == apiKey && x.Expires > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
        if (key == null) return [];

        UserModel[] allPossibleModels = await GetValidModelsByUserId(key.UserId).ToArrayAsync(cancellationToken);
        if (key.AllowAllModels)
        {
            return allPossibleModels;
        }
        else
        {
            HashSet<short> selectedModels = key.Models.Select(x => x.Id).ToHashSet();
            return allPossibleModels
                .Where(x => selectedModels.Contains(x.ModelId))
                .ToArray();
        }
    }
}
