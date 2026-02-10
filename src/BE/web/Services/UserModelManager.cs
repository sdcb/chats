using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class UserModelManager(ChatsDB db)
{
    private static IQueryable<UserApiKey> ActiveApiKeysQuery(IQueryable<UserApiKey> query)
    {
        return query.Where(x => !x.IsDeleted && !x.IsRevoked && x.Expires > DateTime.UtcNow);
    }

    private async Task<UserApiKey?> GetActiveApiKeyByValue(string apiKey, CancellationToken cancellationToken)
    {
        return await ActiveApiKeysQuery(db.UserApiKeys.Include(x => x.Models))
            .FirstOrDefaultAsync(x => x.Key == apiKey, cancellationToken);
    }

    private async Task<UserApiKey?> GetActiveApiKeyById(int apiKeyId, CancellationToken cancellationToken)
    {
        return await ActiveApiKeysQuery(db.UserApiKeys.Include(x => x.Models))
            .FirstOrDefaultAsync(x => x.Id == apiKeyId, cancellationToken);
    }

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

    private async Task<UserModel?> GetUserModelByUserIdAndName(int userId, string modelName, CancellationToken cancellationToken)
    {
        UserModel? balances = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Where(x => x.UserId == userId && !x.Model.IsDeleted && x.Model.Name == modelName)
            .FirstOrDefaultAsync(cancellationToken);

        return balances;
    }

    public async Task<UserModel?> GetUserModel(string apiKey, string modelName, CancellationToken cancellationToken)
    {
        UserApiKey? key = await GetActiveApiKeyByValue(apiKey, cancellationToken);
        if (key == null) return null;

        return await GetUserModelByApiKeyEntity(key, modelName, cancellationToken);
    }

    public async Task<UserModel?> GetUserModel(int apiKeyId, string modelName, CancellationToken cancellationToken)
    {
        UserApiKey? key = await GetActiveApiKeyById(apiKeyId, cancellationToken);
        if (key == null) return null;

        return await GetUserModelByApiKeyEntity(key, modelName, cancellationToken);
    }

    private async Task<UserModel?> GetUserModelByApiKeyEntity(UserApiKey key, string modelName, CancellationToken cancellationToken)
    {
        UserModel? userModel = await GetUserModelByUserIdAndName(key.UserId, modelName, cancellationToken);
        if (key.AllowAllModels || userModel != null && key.Models.Select(x => x.Id).Contains(userModel.ModelId))
        {
            return userModel;
        }
        else
        {
            return null;
        }
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
        UserApiKey? key = await GetActiveApiKeyByValue(apiKey, cancellationToken);
        if (key == null) return [];

        return await GetValidModelsByApiKeyEntity(key, cancellationToken);
    }

    public async Task<UserModel[]> GetValidModelsByApiKeyId(int apiKeyId, CancellationToken cancellationToken)
    {
        UserApiKey? key = await GetActiveApiKeyById(apiKeyId, cancellationToken);
        if (key == null) return [];

        return await GetValidModelsByApiKeyEntity(key, cancellationToken);
    }

    private async Task<UserModel[]> GetValidModelsByApiKeyEntity(UserApiKey key, CancellationToken cancellationToken)
    {
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
