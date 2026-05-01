using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class UserModelManager(ChatsDB db)
{
    private static IQueryable<UserModel> IncludeModelSnapshotChain(IQueryable<UserModel> query)
    {
        return query
            .Include(x => x.Model)
                .ThenInclude(x => x.CurrentSnapshot)
                    .ThenInclude(x => x.ModelKeySnapshot)
                        .ThenInclude(x => x.ModelKey);
    }

    public async Task<UserModel?> GetUserModel(int userId, short modelId, CancellationToken cancellationToken)
    {
        UserModel? balances = await IncludeModelSnapshotChain(db.UserModels)
            .Where(x => x.UserId == userId && x.Model.Enabled && x.ModelId == modelId)
            .FirstOrDefaultAsync(cancellationToken);

        return balances;
    }

    public async Task<Dictionary<short, UserModel>> GetUserModels(int userId, HashSet<short> modelIds, CancellationToken cancellationToken)
    {
        Dictionary<short, UserModel> balances = await IncludeModelSnapshotChain(db.UserModels)
            .Where(x => x.UserId == userId && x.Model.Enabled && modelIds.Contains(x.ModelId))
            .ToDictionaryAsync(k => k.ModelId, v => v, cancellationToken);

        return balances;
    }

    private async Task<UserModel?> GetUserModel(int userId, string modelName, CancellationToken cancellationToken)
    {
        UserModel? balances = await IncludeModelSnapshotChain(db.UserModels)
            .Where(x => x.UserId == userId && x.Model.Enabled && x.Model.CurrentSnapshot.Name == modelName)
            .FirstOrDefaultAsync(cancellationToken);

        return balances;
    }

    public async Task<UserModel?> GetUserModel(string apiKey, string modelName, CancellationToken cancellationToken)
    {
        UserApiKey? key = await db.UserApiKeys
            .Include(x => x.Models)
            .Where(x => x.Key == apiKey && x.Expires > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
        if (key == null) return null;

        UserModel? userModel = await GetUserModel(key.UserId, modelName, cancellationToken);
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
        return IncludeModelSnapshotChain(db.UserModels)
            .Where(x => x.UserId == userId && x.Model.Enabled)
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
