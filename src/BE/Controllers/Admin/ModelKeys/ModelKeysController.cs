using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
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

        // 验证 ModelProviderId 是否有效
        if (!Enum.IsDefined(typeof(DBModelProvider), (int)request.ModelProviderId))
        {
            return BadRequest("Invalid model provider");
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
        // 验证 ModelProviderId 是否有效
        if (!Enum.IsDefined(typeof(DBModelProvider), (int)request.ModelProviderId))
        {
            return BadRequest("Invalid model provider");
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
            return BadRequest("Model key is in use");
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
        
        // 所有provider都走loader，不支持的会抛出异常
        ModelLoader loader = cf.CreateModelLoader(modelProvider);
        string[] models = await loader.ListModels(modelKey, cancellationToken);
        
        // 构建 deploymentName -> Model 的映射
        Dictionary<string, Model[]> existingModelsMap = modelKey.Models
            .GroupBy(x => x.DeploymentName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, v => v.ToArray());

        PossibleModelDto[] result = [.. models.Select(model => 
        {
            AdminModelDto? existingModelDto = null;
            if (existingModelsMap.TryGetValue(model, out Model[]? existingModels))
            {
                Model existingModel = existingModels[0];

                existingModelDto = new AdminModelDto
                {
                    ModelId = existingModel.Id,
                    Name = existingModel.Name,
                    Enabled = !existingModel.IsDeleted,
                    ModelKeyId = existingModel.ModelKeyId,
                    ModelProviderId = existingModel.ModelKey.ModelProviderId,
                    InputTokenPrice1M = existingModel.InputTokenPrice1M,
                    OutputTokenPrice1M = existingModel.OutputTokenPrice1M,
                    DeploymentName = existingModel.DeploymentName,
                    AllowSearch = existingModel.AllowSearch,
                    AllowVision = existingModel.AllowVision,
                    AllowStreaming = existingModel.AllowStreaming,
                    AllowCodeExecution = existingModel.AllowCodeExecution,
                    ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(existingModel.ReasoningEffortOptions),
                    MinTemperature = existingModel.MinTemperature,
                    MaxTemperature = existingModel.MaxTemperature,
                    ContextWindow = existingModel.ContextWindow,
                    MaxResponseTokens = existingModel.MaxResponseTokens,
                    AllowToolCall = existingModel.AllowToolCall,
                    SupportedImageSizes = Model.GetSupportedImageSizesAsArray(existingModel.SupportedImageSizes),
                    ApiType = (DBApiType)existingModel.ApiType,
                    UseAsyncApi = existingModel.UseAsyncApi,
                    UseMaxCompletionTokens = existingModel.UseMaxCompletionTokens,
                    IsLegacy = existingModel.IsLegacy,
                    ThinkTagParserEnabled = existingModel.ThinkTagParserEnabled,
                    MaxThinkingBudget = existingModel.MaxThinkingBudget,
                };
            }
            
            return new PossibleModelDto
            {
                DeploymentName = model,
                ExistingModel = existingModelDto,
            };
        })];

        return Ok(result);
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
        if (previousModelKey != null && nextModelKey != null && previousModelKey.Order > nextModelKey.Order)
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
}
