using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.AdminModels;

[Route("api/admin/models"), AuthorizeAdmin]
public class AdminModelsController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminModelDto[]>> GetAdminModels(bool all, CancellationToken cancellationToken)
    {
        IQueryable<Model> query = db.Models;
        if (!all) query = query.Where(x => x.Enabled);

        AdminModelDto[] data = await (
            from m in query
            join mpo in db.ModelProviderOrders on m.CurrentSnapshot.ModelKeySnapshot.ModelProviderId equals mpo.ModelProviderId into mpoGroup
            from mpo in mpoGroup.DefaultIfEmpty()
            orderby mpo != null ? mpo.Order : int.MaxValue, m.CurrentSnapshot.ModelKeySnapshot.ModelKey!.Order, m.Order
            select m
        )
        .Select(x => new AdminModelDto
            {
                ModelId = x.Id,
                Name = x.CurrentSnapshot.Name,
                Enabled = x.Enabled,
                ModelKeyId = x.CurrentSnapshot.ModelKeyId,
                ModelProviderId = x.CurrentSnapshot.ModelKeySnapshot.ModelProviderId,
                InputFreshTokenPrice1M = x.CurrentSnapshot.InputFreshTokenPrice1M,
                OutputTokenPrice1M = x.CurrentSnapshot.OutputTokenPrice1M,
                InputCachedTokenPrice1M = x.CurrentSnapshot.InputCachedTokenPrice1M,
                DeploymentName = x.CurrentSnapshot.DeploymentName,
                AllowSearch = x.CurrentSnapshot.AllowSearch,
                AllowVision = x.CurrentSnapshot.AllowVision,
                AllowStreaming = x.CurrentSnapshot.AllowStreaming,
                AllowCodeExecution = x.CurrentSnapshot.AllowCodeExecution,
                ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(x.CurrentSnapshot.ReasoningEffortOptions),
                MinTemperature = x.CurrentSnapshot.MinTemperature,
                MaxTemperature = x.CurrentSnapshot.MaxTemperature,
                ContextWindow = x.CurrentSnapshot.ContextWindow,
                MaxResponseTokens = x.CurrentSnapshot.MaxResponseTokens,
                AllowToolCall = x.CurrentSnapshot.AllowToolCall,
                SupportedImageSizes = Model.GetSupportedImageSizesAsArray(x.CurrentSnapshot.SupportedImageSizes),
                ApiType = (DBApiType)x.CurrentSnapshot.ApiTypeId,
                UseAsyncApi = x.CurrentSnapshot.UseAsyncApi,
                UseMaxCompletionTokens = x.CurrentSnapshot.UseMaxCompletionTokens,
                IsLegacy = x.CurrentSnapshot.IsLegacy,
                ThinkTagParserEnabled = x.CurrentSnapshot.ThinkTagParserEnabled,
                MaxThinkingBudget = x.CurrentSnapshot.MaxThinkingBudget,
                SupportsVisionLink = x.CurrentSnapshot.SupportsVisionLink,
        })
            .ToArrayAsync(cancellationToken);
        return data;
    }

    [HttpPut("{modelId:int}")]
    public async Task<ActionResult> UpdateModel(short modelId, [FromBody] UpdateModelRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Model? cm = await db.Models
            .Include(x => x.CurrentSnapshot)
            .FirstOrDefaultAsync(x => x.Id == modelId, cancellationToken);
        if (cm == null) return NotFound();

        ModelKey? modelKey = await db.ModelKeys
            .Include(x => x.CurrentSnapshot)
            .SingleOrDefaultAsync(x => x.Id == req.ModelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return BadRequest($"Model key id: {req.ModelKeyId} not found");
        }

        if (!req.Matches(cm, modelKey))
        {
            DateTime now = DateTime.UtcNow;
            req.ApplyTo(cm);
            cm.CurrentSnapshot = req.ToSnapshot(cm.Id, modelKey, now);
            cm.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateModel([FromBody] UpdateModelRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        DateTime now = DateTime.UtcNow;
        ModelKey? modelKey = await db.ModelKeys
            .Include(x => x.CurrentSnapshot)
            .SingleOrDefaultAsync(x => x.Id == req.ModelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return BadRequest($"Model key id: {req.ModelKeyId} not found");
        }

        Model toCreate = new()
        {
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshot = req.ToSnapshot(0, modelKey, now),
        };
        req.ApplyTo(toCreate);
        db.Models.Add(toCreate);
        await db.SaveChangesAsync(cancellationToken);

        toCreate.CurrentSnapshot.ModelId = toCreate.Id;
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), toCreate.Id);
    }

    [HttpDelete("{modelId:int}")]
    public async Task<ActionResult<DeleteModelResponse>> DeleteModel(short modelId, CancellationToken cancellationToken)
    {
        Model? cm = await db.Models
            .Include(x => x.ApiKeys)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == modelId, cancellationToken);
        if (cm == null) return NotFound();

        bool hasChatConfigs = await db.ChatConfigs.AnyAsync(x => x.ModelId == modelId, cancellationToken);
        if (hasChatConfigs)
        {
            cm.Enabled = false;
            cm.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            
            return Ok(DeleteModelResponse.CreateSoftDeleted());
        }

        db.UserModels.RemoveRange(db.UserModels.Where(x => x.ModelId == modelId));
        db.UserApiCaches.RemoveRange(db.UserApiCaches.Where(x => x.ModelId == modelId));
        cm.ApiKeys.Clear();

        db.Models.Remove(cm);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(DeleteModelResponse.CreateHardDeleted());
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ModelValidateResult>> ValidateModel(
        [FromBody] UpdateModelRequest req,
        [FromServices] ChatFactory chatFactory,
        [FromServices] FileUrlProvider fup,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        ModelKey? modelKey = await db.ModelKeys
            .Where(x => x.Id == req.ModelKeyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (modelKey == null)
        {
            return BadRequest($"Model key id: {req.ModelKeyId} not found");
        }

        // 创建临时 Model 对象用于验证
        DateTime now = DateTime.UtcNow;
        Model tempModel = new()
        {
            Enabled = req.Enabled,
            CurrentSnapshot = req.ToSnapshot(0, modelKey, now),
        };

        ModelValidateResult result = await chatFactory.ValidateModel(tempModel, fup, cancellationToken);
        return Ok(result);
    }

    [HttpPut("reorder")]
    public async Task<ActionResult> ReorderModels([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 验证被移动的 Model 是否存在
        Model? sourceModel = await db.Models
            .Include(x => x.CurrentSnapshot)
            .FirstOrDefaultAsync(x => x.Id == request.SourceId, cancellationToken);
        if (sourceModel == null)
        {
            return NotFound("Source model not found");
        }

        // 验证 previous 和 next 的 Model 是否存在（如果提供的话）
        Model? previousModel = null;
        Model? nextModel = null;

        if (request.PreviousId != null)
        {
            previousModel = await db.Models
                .Include(x => x.CurrentSnapshot)
                .FirstOrDefaultAsync(x => x.Id == request.PreviousId, cancellationToken);
            if (previousModel == null)
            {
                return NotFound("Previous model not found");
            }
            
            // 验证 previous 与 source 属于同一个 ModelKey
            if (previousModel.CurrentSnapshot.ModelKeyId != sourceModel.CurrentSnapshot.ModelKeyId)
            {
                return BadRequest("Previous model must belong to the same ModelKey");
            }
        }

        if (request.NextId != null)
        {
            nextModel = await db.Models
                .Include(x => x.CurrentSnapshot)
                .FirstOrDefaultAsync(x => x.Id == request.NextId, cancellationToken);
            if (nextModel == null)
            {
                return NotFound("Next model not found");
            }
            
            // 验证 next 与 source 属于同一个 ModelKey
            if (nextModel.CurrentSnapshot.ModelKeyId != sourceModel.CurrentSnapshot.ModelKeyId)
            {
                return BadRequest("Next model must belong to the same ModelKey");
            }
        }

        // 验证 previous 和 next 不能同时为空
        if (previousModel == null && nextModel == null)
        {
            return BadRequest("Both previous and next models cannot be null");
        }

        // 验证 previous 和 next 的顺序（Order 是从小到大排列的）
        if (previousModel != null && nextModel != null && previousModel.Order > nextModel.Order)
        {
            return BadRequest("Invalid order: previous model should have smaller order than next model");
        }

        // 尝试应用移动
        bool needReorder = !TryApplyMove(sourceModel, previousModel, nextModel);
        
        if (needReorder)
        {
            // 需要重新排序，但只重排序同一个 ModelKey 下的 Models
            Model[] modelsInSameKey = await db.Models
                .Include(x => x.CurrentSnapshot)
                .Where(x => x.CurrentSnapshot.ModelKeyId == sourceModel.CurrentSnapshot.ModelKeyId)
                .OrderBy(x => x.Order).ThenByDescending(x => x.Id)
                .ToArrayAsync(cancellationToken);
            
            ReorderModels(modelsInSameKey);
            
            // 重新加载并应用移动
            sourceModel = modelsInSameKey.First(x => x.Id == request.SourceId);
            previousModel = request.PreviousId != null ? modelsInSameKey.First(x => x.Id == request.PreviousId) : null;
            nextModel = request.NextId != null ? modelsInSameKey.First(x => x.Id == request.NextId) : null;
            
            TryApplyMove(sourceModel, previousModel, nextModel);
        }

        if (db.ChangeTracker.HasChanges())
        {
            sourceModel.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool TryApplyMove(Model sourceModel, Model? previousModel, Model? nextModel)
    {
        return ReorderHelper.Default.TryApplyMove(sourceModel, previousModel, nextModel);
    }

    private static void ReorderModels(Model[] existingModels)
    {
        ReorderHelper.Default.ReorderEntities(existingModels);
    }
}
