using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.ModelKeys;

[Route("api/admin/model-keys"), AuthorizeAdmin]
public class ModelKeysController(ChatsDB db) : ControllerBase
{
    private const short RankStep = 1000;
    private const short RankStart = -30000;

    [HttpGet]
    public async Task<ActionResult<ModelKeyDto[]>> GetAllModelKeys(CancellationToken cancellationToken)
    {
        ModelKeyDto[] result = await db.ModelKeys
            .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
            .Select(x => new ModelKeyDto
            {
                Id = x.Id,
                ModelProviderId = x.ModelProviderId,
                Name = x.Name,
                Host = x.Host,
                Secret = x.Secret,
                CreatedAt = x.CreatedAt,
                EnabledModelCount = x.Models.Count(x => !x.IsDeleted),
                TotalModelCount = x.Models.Count
            })
            .ToArrayAsync(cancellationToken);

        for (int i = 0; i < result.Length; i++)
        {
            ModelKeyDto modelKey = result[i];
            result[i] = modelKey.WithMaskedKeys();
        }

        return Ok(result);
    }

    [HttpPut("{modelKeyId}")]
    public async Task<ActionResult> UpdateModelKey(short modelKeyId, [FromBody] UpdateModelKeyRequest request, CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return NotFound();
        }

        if (!await db.ModelProviders.AnyAsync(x => x.Id == request.ModelProviderId, cancellationToken))
        {
            return this.BadRequestMessage("Invalid model provider");
        }
        modelKey.ModelProviderId = request.ModelProviderId;
        modelKey.Name = request.Name;
        if (!modelKey.Secret.IsMaskedEquals(request.Secret))
        {
            modelKey.Secret = request.Secret;
        }
        modelKey.Host = request.Host;
        if (db.ChangeTracker.HasChanges())
        {
            modelKey.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> CreateModelKey([FromBody] UpdateModelKeyRequest request, CancellationToken cancellationToken)
    {
        if (!await db.ModelProviders.AnyAsync(x => x.Id == request.ModelProviderId, cancellationToken))
        {
            return this.BadRequestMessage("Invalid model provider");
        }

        // 获取当前最大的 Order 值，以便将新的 ModelKey 放置在最后
        short maxOrder = await db.ModelKeys
            .OrderByDescending(x => x.Order)
            .Select(x => x.Order)
            .FirstOrDefaultAsync(cancellationToken);

        // 计算新的 Order 值
        int newOrder = maxOrder + RankStep;
        
        // 如果新的 Order 值超出了 short 的范围，需要重新排序所有 ModelKey
        if (newOrder > short.MaxValue)
        {
            ModelKey[] allModelKeys = await db.ModelKeys
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderModelKeys(allModelKeys);
            await db.SaveChangesAsync(cancellationToken);
            
            // 重新计算新的 Order 值
            newOrder = RankStart + allModelKeys.Length * RankStep;
        }

        ModelKey newModelKey = new()
        {
            ModelProviderId = request.ModelProviderId,
            Name = request.Name,
            Host = request.Host,
            Secret = request.Secret,
            Order = (short)newOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ModelKeys.Add(newModelKey);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), value: newModelKey.Id);
    }

    [HttpDelete("{modelKeyId}")]
    public async Task<ActionResult> DeleteModelKey(short modelKeyId, CancellationToken cancellationToken)
    {
        if (await db.Models.AnyAsync(m => m.ModelKeyId == modelKeyId, cancellationToken))
        {
            return this.BadRequestMessage("Model key is in use");
        }

        ModelKey? modelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return NotFound();
        }

        db.ModelKeys.Remove(modelKey);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("{modelKeyId:int}/possible-models")]
    public async Task<ActionResult<PossibleModelDto[]>> ListModelKeyPossibleModels(short modelKeyId, [FromServices] ChatFactory cf, CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db
           .ModelKeys
           .Include(x => x.Models)
           .AsSplitQuery()
           .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);

        if (modelKey == null)
        {
            return NotFound();
        }

        DBModelProvider modelProvider = (DBModelProvider)modelKey.ModelProviderId;
        ModelLoader? loader = cf.CreateModelLoader(modelProvider);
        if (loader != null)
        {
            string[] models = await loader.ListModels(modelKey, cancellationToken);
            HashSet<string> existsDeploymentNames = await db.Models
                .Where(x => x.ModelKeyId == modelKeyId && x.DeploymentName != null)
                .Select(x => x.DeploymentName!)
                .ToHashSetAsync(cancellationToken);

            if (modelProvider == DBModelProvider.Ollama || modelProvider == DBModelProvider.OpenRouter)
            {
                Dictionary<short, ModelReference> referenceOptions = await db.ModelReferences
                    .Where(x => x.ProviderId == modelKey.ModelProviderId)
                    .ToDictionaryAsync(k => k.Id, v => v, cancellationToken);

                return Ok(models.Select(model =>
                {
                    bool isVision = 
                        model.Contains("qvq", StringComparison.OrdinalIgnoreCase) ||
                        model.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                        model.Contains("vl", StringComparison.OrdinalIgnoreCase);
                    // 1401, 1400 -> ollama, 1801, 1800 -> openrouter
                    short modelReferenceId = (short)((int)modelProvider * 100 + (isVision ? 1 : 0));
                    return new PossibleModelDto()
                    {
                        DeploymentName = model,
                        ReferenceId = modelReferenceId,
                        ReferenceName = referenceOptions[modelReferenceId].Name,
                        IsLegacy = referenceOptions[modelReferenceId].PublishDate switch
                        {
                            var x when x < new DateOnly(2024, 7, 1) => true,
                            _ => false
                        },
                        IsExists = existsDeploymentNames.Contains(model),
                    };
                }));
            }
            else
            {
                Dictionary<string, ModelReference> referenceOptions = await db.ModelReferences
                    .Where(x => x.ProviderId == modelKey.ModelProviderId)
                    .ToDictionaryAsync(k => k.Name, v => v, cancellationToken);
                HashSet<string> referenceOptionNames = [.. referenceOptions.Keys];

                return Ok(models.Select(model =>
                {
                    string bestMatch = FuzzyMatcher.FindBestMatch(model, referenceOptionNames);

                    return new PossibleModelDto()
                    {
                        DeploymentName = model,
                        ReferenceId = referenceOptions[bestMatch].Id,
                        ReferenceName = referenceOptions[bestMatch].Name,
                        IsLegacy = false,
                        IsExists = existsDeploymentNames.Contains(model),
                    };
                }).ToArray());
            }
        }
        else
        {
            PossibleModelDto[] readyRefs = await db.ModelReferences
                .Where(x => x.ProviderId == modelKey.ModelProviderId)
                .OrderBy(x => x.Name)
                .Select(x => new PossibleModelDto()
                {
                    DeploymentName = x.Models.FirstOrDefault(m => m.ModelKeyId == modelKeyId)!.DeploymentName,
                    ReferenceId = x.Id,
                    ReferenceName = x.Name,
                    IsLegacy = x.PublishDate != null && x.PublishDate < new DateOnly(2024, 7, 1),
                    IsExists = x.Models.Any(m => m.ModelKeyId == modelKeyId),
                })
                .OrderBy(x => (x.IsLegacy ? 1 : 0) + (x.IsExists ? 2 : 0))
                .ThenByDescending(x => x.ReferenceId)
                .ToArrayAsync(cancellationToken);

            return Ok(readyRefs);
        }
    }

    [HttpPut("reorder")]
    public async Task<ActionResult> ReorderModelKeys([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 验证被移动的 ModelKey 是否存在
        ModelKey? sourceModelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == request.SourceId, cancellationToken);
        if (sourceModelKey == null)
        {
            return NotFound("Source model key not found");
        }

        // 验证 before 和 after 的 ModelKey 是否存在（如果提供的话）
        ModelKey? beforeModelKey = null;
        ModelKey? afterModelKey = null;

        if (request.BeforeId != null)
        {
            beforeModelKey = await db.ModelKeys
                .FirstOrDefaultAsync(x => x.Id == request.BeforeId, cancellationToken);
            if (beforeModelKey == null)
            {
                return NotFound("Before model key not found");
            }
        }

        if (request.AfterId != null)
        {
            afterModelKey = await db.ModelKeys
                .FirstOrDefaultAsync(x => x.Id == request.AfterId, cancellationToken);
            if (afterModelKey == null)
            {
                return NotFound("After model key not found");
            }
        }

        // 验证 before 和 after 不能同时为空
        if (beforeModelKey == null && afterModelKey == null)
        {
            return BadRequest("Both before and after model keys cannot be null");
        }

        // 验证 before 和 after 的顺序（Order 是从小到大排列的）
        if (beforeModelKey != null && afterModelKey != null && beforeModelKey.Order >= afterModelKey.Order)
        {
            return BadRequest("Invalid order: before model key should have smaller order than after model key");
        }

        // 尝试应用移动
        bool needReorder = !TryApplyMove(sourceModelKey, beforeModelKey, afterModelKey);
        
        if (needReorder)
        {
            // 需要重新排序所有 ModelKey
            ModelKey[] allModelKeys = await db.ModelKeys
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderModelKeys(allModelKeys);
            
            // 重新加载并应用移动
            sourceModelKey = allModelKeys.First(x => x.Id == request.SourceId);
            beforeModelKey = request.BeforeId != null ? allModelKeys.First(x => x.Id == request.BeforeId) : null;
            afterModelKey = request.AfterId != null ? allModelKeys.First(x => x.Id == request.AfterId) : null;
            
            TryApplyMove(sourceModelKey, beforeModelKey, afterModelKey);
        }

        if (db.ChangeTracker.HasChanges())
        {
            sourceModelKey.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool TryApplyMove(ModelKey sourceModelKey, ModelKey? beforeModelKey, ModelKey? afterModelKey)
    {
        // 计算新的 Order 值
        int newOrder = 0;
        if (beforeModelKey != null && afterModelKey != null)
        {
            // 在两个 ModelKey 之间插入
            if (beforeModelKey.Order + 1 >= afterModelKey.Order)
            {
                return false; // 没有足够的空间，需要重新排序
            }
            newOrder = (beforeModelKey.Order + afterModelKey.Order) / 2;
        }
        else if (beforeModelKey != null)
        {
            // 插入到 before 之后
            newOrder = beforeModelKey.Order + RankStep;
        }
        else if (afterModelKey != null)
        {
            // 插入到 after 之前
            newOrder = afterModelKey.Order - RankStep;
        }

        // 检查新的 Order 值是否在有效范围内
        if (newOrder > short.MaxValue || newOrder < short.MinValue)
        {
            return false;
        }

        sourceModelKey.Order = (short)newOrder;
        return true;
    }

    private static void ReorderModelKeys(ModelKey[] existingModelKeys)
    {
        for (int i = 0; i < existingModelKeys.Length; i++)
        {
            existingModelKeys[i].Order = (short)(RankStart + i * RankStep);
        }
    }

    [HttpPut("reorder-model-providers")]
    public async Task<ActionResult> ReorderModelProviders([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 验证被移动的 ModelProvider 是否存在
        ModelProvider? sourceProvider = await db.ModelProviders
            .FirstOrDefaultAsync(x => x.Id == request.SourceId, cancellationToken);
        if (sourceProvider == null)
        {
            return NotFound("Source model provider not found");
        }

        // 验证 before 和 after 的 ModelProvider 是否存在（如果提供的话）
        ModelProvider? beforeProvider = null;
        ModelProvider? afterProvider = null;

        if (request.BeforeId != null)
        {
            beforeProvider = await db.ModelProviders
                .FirstOrDefaultAsync(x => x.Id == request.BeforeId, cancellationToken);
            if (beforeProvider == null)
            {
                return NotFound("Before model provider not found");
            }
        }

        if (request.AfterId != null)
        {
            afterProvider = await db.ModelProviders
                .FirstOrDefaultAsync(x => x.Id == request.AfterId, cancellationToken);
            if (afterProvider == null)
            {
                return NotFound("After model provider not found");
            }
        }

        // 验证 before 和 after 不能同时为空
        if (beforeProvider == null && afterProvider == null)
        {
            return BadRequest("Both before and after model providers cannot be null");
        }

        // 获取所有 ModelKey 并按当前顺序排列
        ModelKey[] allModelKeys = await db.ModelKeys
            .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
            .ToArrayAsync(cancellationToken);

        // 按 ModelProviderId 分组，获取每个 ModelProvider 的第一个 ModelKey 作为代表
        Dictionary<short, ModelKey> providerRepresentatives = allModelKeys
            .GroupBy(x => x.ModelProviderId)
            .ToDictionary(g => g.Key, g => g.First());

        // 获取当前 ModelProvider 的顺序（基于其代表 ModelKey 的 Order）
        List<short> currentProviderOrder = [.. providerRepresentatives.Values
            .OrderBy(x => x.Order)
            .Select(x => x.ModelProviderId)];

        // 验证 before 和 after 的顺序
        if (beforeProvider != null && afterProvider != null)
        {
            int beforeIndex = currentProviderOrder.IndexOf(beforeProvider.Id);
            int afterIndex = currentProviderOrder.IndexOf(afterProvider.Id);
            if (beforeIndex >= afterIndex)
            {
                return BadRequest("Invalid order: before model provider should appear before after model provider");
            }
        }

        // 计算目标位置
        int targetIndex = 0;
        if (beforeProvider != null && afterProvider != null)
        {
            int beforeIndex = currentProviderOrder.IndexOf(beforeProvider.Id);
            int afterIndex = currentProviderOrder.IndexOf(afterProvider.Id);
            targetIndex = beforeIndex + 1;
        }
        else if (beforeProvider != null)
        {
            int beforeIndex = currentProviderOrder.IndexOf(beforeProvider.Id);
            targetIndex = beforeIndex + 1;
        }
        else if (afterProvider != null)
        {
            int afterIndex = currentProviderOrder.IndexOf(afterProvider.Id);
            targetIndex = afterIndex;
        }

        // 重新排列 ModelProvider 顺序
        List<short> newProviderOrder = [.. currentProviderOrder];
        newProviderOrder.Remove(request.SourceId);
        newProviderOrder.Insert(targetIndex, request.SourceId);

        // 计算每个 ModelProvider 组应该的 Order 范围
        Dictionary<short, (short minOrder, short maxOrder)> providerOrderRanges = [];
        for (int i = 0; i < newProviderOrder.Count; i++)
        {
            short providerId = newProviderOrder[i];
            short minOrder = (short)(RankStart + i * RankStep * 100); // 给每个 Provider 预留 100 个 RankStep 的空间
            short maxOrder = (short)(minOrder + RankStep * 100 - 1);
            providerOrderRanges[providerId] = (minOrder, maxOrder);
        }

        // 检查是否需要重新排序（如果范围超出 short 的最大值）
        bool needGlobalReorder = providerOrderRanges.Values.Any(range => range.maxOrder > short.MaxValue);

        if (needGlobalReorder)
        {
            // 全局重新排序，压缩范围
            providerOrderRanges.Clear();
            int stepPerProvider = Math.Max(1, (short.MaxValue - RankStart) / (newProviderOrder.Count * 100));
            for (int i = 0; i < newProviderOrder.Count; i++)
            {
                short providerId = newProviderOrder[i];
                short minOrder = (short)(RankStart + i * stepPerProvider * 100);
                short maxOrder = (short)(minOrder + stepPerProvider * 100 - 1);
                providerOrderRanges[providerId] = (minOrder, maxOrder);
            }
        }

        // 重新分配每个 ModelKey 的 Order
        foreach (IGrouping<short, ModelKey> providerGroup in allModelKeys.GroupBy(x => x.ModelProviderId))
        {
            short providerId = providerGroup.Key;
            if (!providerOrderRanges.TryGetValue(providerId, out (short minOrder, short maxOrder) range))
            {
                continue; // 跳过不存在的 Provider
            }

            ModelKey[] providerModelKeys = providerGroup.OrderBy(x => x.Order).ThenByDescending(x => x.Id).ToArray();
            for (int i = 0; i < providerModelKeys.Length; i++)
            {
                // 在 Provider 的范围内均匀分布 ModelKey
                int stepSize = Math.Max(1, (range.maxOrder - range.minOrder + 1) / Math.Max(1, providerModelKeys.Length));
                short newOrder = (short)(range.minOrder + i * stepSize);
                providerModelKeys[i].Order = newOrder;
                providerModelKeys[i].UpdatedAt = DateTime.UtcNow;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}
