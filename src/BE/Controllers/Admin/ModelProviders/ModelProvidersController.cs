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
        // 单次查询（EF 可翻译）：先将 GroupBy 投影为可连接的匿名对象，再 LEFT JOIN 排序，并使用相关子查询统计模型数量
        var providersWithKeyCounts =
            from mk in db.ModelKeys
            group mk by mk.ModelProviderId into g
            select new { ProviderId = g.Key, KeyCount = g.Count() };

        ModelProviderDto[] data = await (
            from k in providersWithKeyCounts
            join mpo in db.ModelProviderOrders on k.ProviderId equals mpo.ModelProviderId into j
            from mpo in j.DefaultIfEmpty()
            let sortOrder = (int?)mpo.Order
            let modelCount = db.Models.Count(m => !m.IsDeleted && m.ModelKey.ModelProviderId == k.ProviderId)
            orderby (sortOrder ?? short.MaxValue), k.ProviderId
            select new ModelProviderDto
            {
                ProviderId = k.ProviderId,
                KeyCount = k.KeyCount,
                ModelCount = modelCount
            }
        ).ToArrayAsync(cancellationToken);

        return Ok(data);
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
                AllowCodeExecution = x.AllowCodeExecution,
                ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(x.ReasoningEffortOptions),
                AllowStreaming = x.AllowStreaming,
                MinTemperature = x.MinTemperature,
                MaxTemperature = x.MaxTemperature,
                ContextWindow = x.ContextWindow,
                MaxResponseTokens = x.MaxResponseTokens,
                AllowToolCall = x.AllowToolCall,
                SupportedImageSizes = Model.GetSupportedImageSizesAsArray(x.SupportedImageSizes),
                InputFreshTokenPrice1M = x.InputFreshTokenPrice1M,
                OutputTokenPrice1M = x.OutputTokenPrice1M,
                InputCachedTokenPrice1M = x.InputCachedTokenPrice1M,
                ApiType = (DBApiType)x.ApiTypeId,
                UseAsyncApi = x.UseAsyncApi,
                UseMaxCompletionTokens = x.UseMaxCompletionTokens,
                IsLegacy = x.IsLegacy,
                ThinkTagParserEnabled = x.ThinkTagParserEnabled,
                MaxThinkingBudget = x.MaxThinkingBudget,
                SupportsVisionLink = x.SupportsVisionLink,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPut("reorder")]
    public async Task<ActionResult> ReorderModelProviders([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 获取所有 ModelProviderOrder
        List<ModelProviderOrder> providerOrders = await db.ModelProviderOrders
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        // 检查是否有新的 Provider 未在数据库中（包括初始化情况）
        List<short> allProviderIds = Enum.GetValues<DBModelProvider>().Select(p => (short)p).ToList();
        HashSet<short> existingProviderIds = providerOrders.Select(p => p.ModelProviderId).ToHashSet();
        List<short> missingProviderIds = allProviderIds.Where(id => !existingProviderIds.Contains(id)).ToList();

        if (missingProviderIds.Count > 0)
        {
            short currentMaxOrder = providerOrders.Count > 0 ? providerOrders.Max(x => x.Order) : (short)-100;

            foreach (var missingId in missingProviderIds)
            {
                currentMaxOrder += 100;
                var newProvider = new ModelProviderOrder 
                { 
                    ModelProviderId = missingId, 
                    Order = currentMaxOrder 
                };
                providerOrders.Add(newProvider);
                db.ModelProviderOrders.Add(newProvider);
            }
            
            // 确保列表按 Order 排序
            providerOrders = providerOrders.OrderBy(x => x.Order).ToList();
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
            ModelProviderOrder? providerOrder = providerOrders.FirstOrDefault(x => x.ModelProviderId == providerId);
            
            if (providerOrder != null)
            {
                providerOrder.Order = (short)(i * 100);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
