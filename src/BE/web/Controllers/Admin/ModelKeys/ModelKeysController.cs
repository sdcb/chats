using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.DB;
using Chats.DB.Enums;

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
                ModelProviderId = x.CurrentSnapshot.ModelProviderId,
                Name = x.CurrentSnapshot.Name,
                Host = x.CurrentSnapshot.Host,
                Secret = x.CurrentSnapshot.Secret,
                CreatedAt = x.CreatedAt,
                EnabledModelCount = db.Models.Count(m => m.Enabled && m.CurrentSnapshot.ModelKeyId == x.Id),
                TotalModelCount = db.Models.Count(m => m.CurrentSnapshot.ModelKeyId == x.Id)
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
            .Include(x => x.CurrentSnapshot)
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
        string? secret = modelKey.CurrentSnapshot.Secret;
        if (!secret.IsMaskedEquals(request.Secret))
        {
            secret = request.Secret;
        }

        if (modelKey.CurrentSnapshot.ModelProviderId != request.ModelProviderId
            || modelKey.CurrentSnapshot.Name != request.Name
            || modelKey.CurrentSnapshot.Host != request.Host
            || modelKey.CurrentSnapshot.Secret != secret)
        {
            DateTime now = DateTime.UtcNow;
            modelKey.CurrentSnapshot = new ModelKeySnapshot
            {
                ModelKeyId = modelKey.Id,
                ModelProviderId = request.ModelProviderId,
                Name = request.Name,
                Host = request.Host,
                Secret = secret,
                CreatedAt = now,
            };
            modelKey.UpdatedAt = now;

            Model[] affectedModels = await db.Models
                .Include(x => x.CurrentSnapshot)
                .Where(x => x.CurrentSnapshot.ModelKeyId == modelKey.Id)
                .ToArrayAsync(cancellationToken);

            foreach (Model model in affectedModels)
            {
                model.CurrentSnapshot = CloneModelSnapshot(model, modelKey, now);
                model.UpdatedAt = now;
            }

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

        DateTime now = DateTime.UtcNow;
        ModelKey newModelKey = new()
        {
            Order = (short)newOrder,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshot = new ModelKeySnapshot
            {
                ModelProviderId = request.ModelProviderId,
                Name = request.Name,
                Host = request.Host,
                Secret = request.Secret,
                CreatedAt = now,
            },
        };
        db.ModelKeys.Add(newModelKey);
        await db.SaveChangesAsync(cancellationToken);

        newModelKey.CurrentSnapshot.ModelKeyId = newModelKey.Id;
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), value: newModelKey.Id);
    }

    [HttpDelete("{modelKeyId}")]
    public async Task<ActionResult> DeleteModelKey(short modelKeyId, CancellationToken cancellationToken)
    {
        if (await db.Models.AnyAsync(m => m.CurrentSnapshot.ModelKeyId == modelKeyId, cancellationToken))
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
        try
        {
            ModelKey? modelKey = await db
               .ModelKeys
               .Include(x => x.CurrentSnapshot)
               .AsSplitQuery()
               .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);

            if (modelKey == null)
            {
                return NotFound();
            }

            DBModelProvider modelProvider = (DBModelProvider)modelKey.CurrentSnapshot.ModelProviderId;

            DBApiType apiType = modelKey.CurrentSnapshot.Host switch
            {
                null => DBApiType.OpenAIChatCompletion,
                var x when x.Contains("anthropic", StringComparison.OrdinalIgnoreCase) => DBApiType.AnthropicMessages,
                var x when x.Contains("claude", StringComparison.OrdinalIgnoreCase) => DBApiType.AnthropicMessages,
                _ => DBApiType.OpenAIChatCompletion,
            };
            DateTime now = DateTime.UtcNow;
            Model dummyModel = new()
            {
                Enabled = true,
                CurrentSnapshot = new ModelSnapshot
                {
                    ModelId = 0,
                    ModelKeyId = modelKey.Id,
                    ModelKeySnapshotId = modelKey.CurrentSnapshotId,
                    ModelKeySnapshot = modelKey.CurrentSnapshot,
                    ApiTypeId = (byte)apiType,
                    Name = "dummy",
                    DeploymentName = "dummy",
                    CreatedAt = now,
                },
            };
            ChatService service = cf.CreateChatService(dummyModel);
            string[] models = await service.ListModels(modelKey.CurrentSnapshot, cancellationToken);
            
            // 构建 deploymentName -> Model 的映射
            Dictionary<string, Model[]> existingModelsMap = await db.Models
                .Include(x => x.CurrentSnapshot)
                .ThenInclude(x => x.ModelKeySnapshot)
                .Where(x => x.CurrentSnapshot.ModelKeyId == modelKey.Id)
                .GroupBy(x => x.CurrentSnapshot.DeploymentName, StringComparer.Ordinal)
                .ToDictionaryAsync(x => x.Key, v => v.ToArray(), cancellationToken);

            PossibleModelDto[] result = [.. models.Select(model => 
            {
                AdminModelDto? existingModelDto = null;
                if (existingModelsMap.TryGetValue(model, out Model[]? existingModels))
                {
                    Model existingModel = existingModels[0];

                    existingModelDto = new AdminModelDto
                    {
                        ModelId = existingModel.Id,
                        Name = existingModel.CurrentSnapshot.Name,
                        Enabled = existingModel.Enabled,
                        ModelKeyId = existingModel.CurrentSnapshot.ModelKeyId,
                        ModelProviderId = existingModel.CurrentSnapshot.ModelKeySnapshot.ModelProviderId,
                        InputFreshTokenPrice1M = existingModel.CurrentSnapshot.InputFreshTokenPrice1M,
                        OutputTokenPrice1M = existingModel.CurrentSnapshot.OutputTokenPrice1M,
                        InputCachedTokenPrice1M = existingModel.CurrentSnapshot.InputCachedTokenPrice1M,
                        DeploymentName = existingModel.CurrentSnapshot.DeploymentName,
                        AllowSearch = existingModel.CurrentSnapshot.AllowSearch,
                        AllowVision = existingModel.CurrentSnapshot.AllowVision,
                        AllowStreaming = existingModel.CurrentSnapshot.AllowStreaming,
                        AllowCodeExecution = existingModel.CurrentSnapshot.AllowCodeExecution,
                        SupportedEfforts = Model.GetSupportedEffortsAsArray(existingModel.CurrentSnapshot.SupportedEfforts),
                        MinTemperature = existingModel.CurrentSnapshot.MinTemperature,
                        MaxTemperature = existingModel.CurrentSnapshot.MaxTemperature,
                        ContextWindow = existingModel.CurrentSnapshot.ContextWindow,
                        MaxResponseTokens = existingModel.CurrentSnapshot.MaxResponseTokens,
                        AllowToolCall = existingModel.CurrentSnapshot.AllowToolCall,
                        SupportedImageSizes = Model.GetSupportedImageSizesAsArray(existingModel.CurrentSnapshot.SupportedImageSizes),
                        ApiType = (DBApiType)existingModel.CurrentSnapshot.ApiTypeId,
                        UseAsyncApi = existingModel.CurrentSnapshot.UseAsyncApi,
                        UseMaxCompletionTokens = existingModel.CurrentSnapshot.UseMaxCompletionTokens,
                        IsLegacy = existingModel.CurrentSnapshot.IsLegacy,
                        ThinkTagParserEnabled = existingModel.CurrentSnapshot.ThinkTagParserEnabled,
                        MaxThinkingBudget = existingModel.CurrentSnapshot.MaxThinkingBudget,
                        SupportsVisionLink = existingModel.CurrentSnapshot.SupportsVisionLink,
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
        catch (Exception ex)
        {
            return BadRequest(ex.ToString());
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

    private static ModelSnapshot CloneModelSnapshot(Model model, ModelKey modelKey, DateTime createdAt)
    {
        ModelSnapshot current = model.CurrentSnapshot;
        return new ModelSnapshot
        {
            ModelId = model.Id,
            Name = current.Name,
            DeploymentName = current.DeploymentName,
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKey.CurrentSnapshotId,
            ModelKeySnapshot = modelKey.CurrentSnapshot,
            ApiTypeId = current.ApiTypeId,
            InputFreshTokenPrice1M = current.InputFreshTokenPrice1M,
            InputCachedTokenPrice1M = current.InputCachedTokenPrice1M,
            OutputTokenPrice1M = current.OutputTokenPrice1M,
            AllowSearch = current.AllowSearch,
            AllowVision = current.AllowVision,
            AllowStreaming = current.AllowStreaming,
            AllowToolCall = current.AllowToolCall,
            AllowCodeExecution = current.AllowCodeExecution,
            ThinkTagParserEnabled = current.ThinkTagParserEnabled,
            MinTemperature = current.MinTemperature,
            MaxTemperature = current.MaxTemperature,
            ContextWindow = current.ContextWindow,
            MaxResponseTokens = current.MaxResponseTokens,
            SupportedEfforts = current.SupportedEfforts,
            SupportedImageSizes = current.SupportedImageSizes,
            UseAsyncApi = current.UseAsyncApi,
            UseMaxCompletionTokens = current.UseMaxCompletionTokens,
            IsLegacy = current.IsLegacy,
            MaxThinkingBudget = current.MaxThinkingBudget,
            SupportsVisionLink = current.SupportsVisionLink,
            CreatedAt = createdAt,
        };
    }
}
