using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.ModelProviders.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.ModelProviders;

[Route("api/admin/model-providers"), AuthorizeAdmin]
public class ModelProvidersController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ModelProviderDto[]>> GetAllModelProviders(CancellationToken cancellationToken)
    {
        // 获取所有 ModelProviderOrder
        var providerOrders = await db.ModelProviderOrders
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        // 如果 ModelProviderOrder 表为空，按 enum 值顺序初始化所有已定义的 Provider
        if (providerOrders.Count == 0)
        {
            var allProviders = Enum.GetValues<DBModelProvider>()
                .Select((p, index) => new ModelProviderOrder 
                { 
                    ModelProviderId = (short)p, 
                    Order = (short)(index * 100) 
                })
                .ToList();
            
            db.ModelProviderOrders.AddRange(allProviders);
            await db.SaveChangesAsync(cancellationToken);
            providerOrders = allProviders.OrderBy(x => x.Order).ToList();
        }

        // 获取每个 Provider 的 Key 数量和 Model 数量
        var keyCountByProvider = await db.ModelKeys
            .GroupBy(x => x.ModelProviderId)
            .Select(g => new { ProviderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProviderId, x => x.Count, cancellationToken);

        var modelCountByProvider = await db.Models
            .Where(m => !m.IsDeleted)
            .GroupBy(m => m.ModelKey.ModelProviderId)
            .Select(g => new { ProviderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProviderId, x => x.Count, cancellationToken);

        var result = providerOrders.Select(po => new ModelProviderDto
        {
            ProviderId = po.ModelProviderId,
            KeyCount = keyCountByProvider.GetValueOrDefault(po.ModelProviderId, 0),
            ModelCount = modelCountByProvider.GetValueOrDefault(po.ModelProviderId, 0)
        }).ToArray();

        return Ok(result);
    }

    [HttpGet("{providerId}/model-keys")]
    public async Task<ActionResult<ModelKeyDto[]>> GetModelKeysByProvider(short providerId, CancellationToken cancellationToken)
    {
        // 验证 ModelProviderId 是否有效
        if (!Enum.IsDefined(typeof(DBModelProvider), (int)providerId))
        {
            return BadRequest("Invalid model provider");
        }

        ModelKeyDto[] result = await db.ModelKeys
            .Where(x => x.ModelProviderId == providerId)
            .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
            .Select(x => new ModelKeyDto
            {
                Id = x.Id,
                ModelProviderId = x.ModelProviderId,
                Name = x.Name,
                Host = x.Host,
                Secret = x.Secret,
                CreatedAt = x.CreatedAt,
                EnabledModelCount = x.Models.Count(m => !m.IsDeleted),
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

    [HttpGet("model-keys/{modelKeyId}/models")]
    public async Task<ActionResult<AdminModelDto[]>> GetModelsByKey(short modelKeyId, CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        
        if (modelKey == null)
        {
            return NotFound();
        }
        
        AdminModelDto[] result = await db.Models
            .Where(x => x.ModelKeyId == modelKeyId)
            .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
            .Select(x => new AdminModelDto
            {
                ModelId = x.Id,
                ModelProviderId = x.ModelKey.ModelProviderId,
                Name = x.Name,
                Enabled = !x.IsDeleted,
                ModelKeyId = x.ModelKeyId,
                DeploymentName = x.DeploymentName,
                AllowSearch = x.AllowSearch,
                AllowVision = x.AllowVision,
                AllowSystemPrompt = x.AllowSystemPrompt,
                AllowCodeExecution = x.AllowCodeExecution,
                ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(x.ReasoningEffortOptions),
                AllowStreaming = x.AllowStreaming,
                MinTemperature = x.MinTemperature,
                MaxTemperature = x.MaxTemperature,
                ContextWindow = x.ContextWindow,
                MaxResponseTokens = x.MaxResponseTokens,
                AllowToolCall = x.AllowToolCall,
                SupportedImageSizes = Model.GetSupportedImageSizesAsArray(x.SupportedImageSizes),
                InputTokenPrice1M = x.InputTokenPrice1M,
                OutputTokenPrice1M = x.OutputTokenPrice1M,
                ApiType = (DBApiType)x.ApiType,
                UseAsyncApi = x.UseAsyncApi,
                UseMaxCompletionTokens = x.UseMaxCompletionTokens,
                IsLegacy = x.IsLegacy,
                ThinkTagParserEnabled = x.ThinkTagParserEnabled,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPut("reorder")]
    public async Task<ActionResult> ReorderModelProviders([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 获取所有 ModelProviderOrder
        var providerOrders = await db.ModelProviderOrders
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        // 如果 ModelProviderOrder 表为空，按 enum 值顺序初始化所有已定义的 Provider
        if (providerOrders.Count == 0)
        {
            var allProviders = Enum.GetValues<DBModelProvider>()
                .Select((p, index) => new ModelProviderOrder 
                { 
                    ModelProviderId = (short)p, 
                    Order = (short)(index * 100) 
                })
                .ToList();
            
            db.ModelProviderOrders.AddRange(allProviders);
            await db.SaveChangesAsync(cancellationToken);
            providerOrders = allProviders.OrderBy(x => x.Order).ToList();
        }

        // 获取当前的排序列表
        List<short> currentProviderOrder = providerOrders.Select(x => x.ModelProviderId).ToList();

        // 从当前列表中移除 source
        List<short> newProviderOrder = new List<short>(currentProviderOrder);
        newProviderOrder.Remove(request.SourceId);

        // 计算插入位置
        int insertIndex = 0;
        if (request.PreviousId != null && request.NextId != null)
        {
            // 插入到 previous 和 next 之间
            int previousIndex = newProviderOrder.IndexOf(request.PreviousId.Value);
            insertIndex = previousIndex + 1;
        }
        else if (request.PreviousId != null)
        {
            // 插入到 previous 之后
            int previousIndex = newProviderOrder.IndexOf(request.PreviousId.Value);
            insertIndex = previousIndex + 1;
        }
        else if (request.NextId != null)
        {
            // 插入到 next 之前
            int nextIndex = newProviderOrder.IndexOf(request.NextId.Value);
            insertIndex = nextIndex;
        }

        // 插入到新位置
        newProviderOrder.Insert(insertIndex, request.SourceId);

        // 重新设置所有 Provider 的 Order 值（使用100的间隔）
        for (int i = 0; i < newProviderOrder.Count; i++)
        {
            short providerId = newProviderOrder[i];
            var providerOrder = providerOrders.FirstOrDefault(x => x.ModelProviderId == providerId);
            
            if (providerOrder != null)
            {
                providerOrder.Order = (short)(i * 100);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
