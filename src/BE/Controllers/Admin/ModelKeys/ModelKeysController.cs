using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using Chats.BE.Controllers.Common;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.ModelKeys;

[Route("api/admin/model-keys"), AuthorizeAdmin]
public class ModelKeysController(ChatsDB db) : ControllerBase
{
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
        int newOrder = maxOrder + ReorderHelper.Default.MoveStep;
        
        // 如果新的 Order 值超出了 short 的范围，需要重新排序所有 ModelKey
        if (newOrder > short.MaxValue)
        {
            ModelKey[] allModelKeys = await db.ModelKeys
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderModelKeys(allModelKeys);
            await db.SaveChangesAsync(cancellationToken);
            
            // 重新获取最大值并计算新的 Order 值
            maxOrder = allModelKeys.Length > 0 ? allModelKeys[^1].Order : ReorderHelper.Default.ReorderStart;
            newOrder = maxOrder + ReorderHelper.Default.MoveStep;
            
            // 如果还是超出范围，使用最大可能值
            if (newOrder > short.MaxValue)
            {
                newOrder = short.MaxValue;
            }
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

        // 验证 previous 和 next 的 ModelKey 是否存在（如果提供的话）
        ModelKey? previousModelKey = null;
        ModelKey? nextModelKey = null;

        if (request.PreviousId != null)
        {
            previousModelKey = await db.ModelKeys
                .FirstOrDefaultAsync(x => x.Id == request.PreviousId, cancellationToken);
            if (previousModelKey == null)
            {
                return NotFound("Previous model key not found");
            }
        }

        if (request.NextId != null)
        {
            nextModelKey = await db.ModelKeys
                .FirstOrDefaultAsync(x => x.Id == request.NextId, cancellationToken);
            if (nextModelKey == null)
            {
                return NotFound("Next model key not found");
            }
        }

        // 验证 previous 和 next 不能同时为空
        if (previousModelKey == null && nextModelKey == null)
        {
            return BadRequest("Both previous and next model keys cannot be null");
        }

        // 验证 previous 和 next 的顺序（Order 是从小到大排列的）
        if (previousModelKey != null && nextModelKey != null && previousModelKey.Order >= nextModelKey.Order)
        {
            return BadRequest("Invalid order: previous model key should have smaller order than next model key");
        }

        // 尝试应用移动
        bool needReorder = !TryApplyMove(sourceModelKey, previousModelKey, nextModelKey);
        
        if (needReorder)
        {
            // 需要重新排序所有 ModelKey
            ModelKey[] allModelKeys = await db.ModelKeys
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderModelKeys(allModelKeys);
            
            // 重新加载并应用移动
            sourceModelKey = allModelKeys.First(x => x.Id == request.SourceId);
            previousModelKey = request.PreviousId != null ? allModelKeys.First(x => x.Id == request.PreviousId) : null;
            nextModelKey = request.NextId != null ? allModelKeys.First(x => x.Id == request.NextId) : null;
            
            TryApplyMove(sourceModelKey, previousModelKey, nextModelKey);
        }

        if (db.ChangeTracker.HasChanges())
        {
            sourceModelKey.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool TryApplyMove(ModelKey sourceModelKey, ModelKey? previousModelKey, ModelKey? nextModelKey)
    {
        return ReorderHelper.Default.TryApplyMove(sourceModelKey, previousModelKey, nextModelKey);
    }

    private static void ReorderModelKeys(ModelKey[] existingModelKeys)
    {
        ReorderHelper.Default.ReorderEntities(existingModelKeys);
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

        // 验证 previous 和 next 的 ModelProvider 是否存在（如果提供的话）
        ModelProvider? previousProvider = null;
        ModelProvider? nextProvider = null;

        if (request.PreviousId != null)
        {
            previousProvider = await db.ModelProviders
                .FirstOrDefaultAsync(x => x.Id == request.PreviousId, cancellationToken);
            if (previousProvider == null)
            {
                return NotFound("Previous model provider not found");
            }
        }

        if (request.NextId != null)
        {
            nextProvider = await db.ModelProviders
                .FirstOrDefaultAsync(x => x.Id == request.NextId, cancellationToken);
            if (nextProvider == null)
            {
                return NotFound("Next model provider not found");
            }
        }

        // 验证 previous 和 next 不能同时为空
        if (previousProvider == null && nextProvider == null)
        {
            return BadRequest("Both previous and next model providers cannot be null");
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

        // 验证 previous 和 next 的顺序
        if (previousProvider != null && nextProvider != null)
        {
            int previousIndex = currentProviderOrder.IndexOf(previousProvider.Id);
            int nextIndex = currentProviderOrder.IndexOf(nextProvider.Id);
            if (previousIndex + 1 != nextIndex)
            {
                return BadRequest("Previous and next model providers must be adjacent");
            }
        }

        // 从当前列表中移除 source
        List<short> newProviderOrder = [.. currentProviderOrder];
        newProviderOrder.Remove(request.SourceId);

        // 计算插入位置
        int insertIndex = 0;
        if (previousProvider != null && nextProvider != null)
        {
            // 插入到 previous 和 next 之间
            int previousIndex = newProviderOrder.IndexOf(previousProvider.Id);
            insertIndex = previousIndex + 1;
        }
        else if (previousProvider != null)
        {
            // 插入到 previous 之后（末尾）
            int previousIndex = newProviderOrder.IndexOf(previousProvider.Id);
            insertIndex = previousIndex + 1;
        }
        else if (nextProvider != null)
        {
            // 插入到 next 之前（开头）
            int nextIndex = newProviderOrder.IndexOf(nextProvider.Id);
            insertIndex = nextIndex;
        }

        // 插入到新位置
        newProviderOrder.Insert(insertIndex, request.SourceId);

        // 计算每个 ModelProvider 组应该的 Order 范围
        Dictionary<short, (short minOrder, short maxOrder)> providerOrderRanges = [];
        int providerSpacing = 1000; // 每个 Provider 之间的间距
        
        for (int i = 0; i < newProviderOrder.Count; i++)
        {
            short providerId = newProviderOrder[i];
            short baseOrder = (short)(ReorderHelper.Default.ReorderStart + i * providerSpacing);
            short minOrder = baseOrder;
            short maxOrder = (short)(baseOrder + providerSpacing - 1);
            
            // 确保不超出 short 的范围
            if (maxOrder > short.MaxValue)
            {
                // 重新计算，压缩间距
                providerSpacing = Math.Max(100, (short.MaxValue - ReorderHelper.Default.ReorderStart) / newProviderOrder.Count);
                break;
            }
            
            providerOrderRanges[providerId] = (minOrder, maxOrder);
        }

        // 如果需要重新计算间距
        if (providerOrderRanges.Count != newProviderOrder.Count)
        {
            providerOrderRanges.Clear();
            for (int i = 0; i < newProviderOrder.Count; i++)
            {
                short providerId = newProviderOrder[i];
                short baseOrder = (short)(ReorderHelper.Default.ReorderStart + i * providerSpacing);
                short minOrder = baseOrder;
                short maxOrder = (short)(baseOrder + providerSpacing - 1);
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

            ModelKey[] providerModelKeys = [.. providerGroup.OrderBy(x => x.Order).ThenByDescending(x => x.Id)];
            
            if (providerModelKeys.Length == 0) continue;
            
            // 使用重排序工具类在指定范围内重新分布
            ReorderHelper.Default.RedistributeInRange(providerModelKeys, range.minOrder, range.maxOrder);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}
