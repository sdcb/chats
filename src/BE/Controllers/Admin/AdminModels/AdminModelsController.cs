using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.DB.Jsons;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.AdminModels;

[Route("api/admin"), AuthorizeAdmin]
public class AdminModelsController(ChatsDB db, CurrentUser adminUser) : ControllerBase
{
    [HttpGet("models")]
    public async Task<ActionResult<AdminModelDto[]>> GetAdminModels(bool all, CancellationToken cancellationToken)
    {
        IQueryable<Model> query = db.Models;
        if (!all) query = query.Where(x => !x.IsDeleted);

        int? fileServiceId = await FileService.GetDefaultId(db, cancellationToken);
        AdminModelDto[] data = await query
            .OrderBy(x => x.ModelKey.Order).ThenBy(x => x.Order)
            .Select(x => new AdminModelDto
            {
                ModelId = x.Id,
                Name = x.Name,
                Enabled = !x.IsDeleted,
                FileServiceId = fileServiceId,
                ModelKeyId = x.ModelKeyId,
                ModelProviderId = x.ModelKey.ModelProviderId,
                ModelReferenceId = x.ModelReferenceId,
                ModelReferenceName = x.ModelReference.Name,
                ModelReferenceShortName = x.ModelReference.DisplayName,
                InputTokenPrice1M = x.InputTokenPrice1M,
                OutputTokenPrice1M = x.OutputTokenPrice1M,
                DeploymentName = x.DeploymentName,
                AllowSearch = x.ModelReference.AllowSearch,
                AllowVision = x.ModelReference.AllowVision,
                AllowStreaming = x.ModelReference.AllowStreaming,
                AllowSystemPrompt = x.ModelReference.AllowSystemPrompt,
                AllowReasoningEffort = ModelReference.SupportReasoningEffort(x.ModelReference.Name),
                MinTemperature = x.ModelReference.MinTemperature,
                MaxTemperature = x.ModelReference.MaxTemperature,
                ContextWindow = x.ModelReference.ContextWindow,
                MaxResponseTokens = x.ModelReference.MaxResponseTokens,
            })
            .ToArrayAsync(cancellationToken);
        return data;
    }

    [HttpPut("models/{modelId:int}")]
    public async Task<ActionResult> UpdateModel(short modelId, [FromBody] UpdateModelRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await db.ModelReferences.AnyAsync(r => r.Id == req.ModelReferenceId, cancellationToken))
        {
            return this.BadRequestMessage($"Invalid ModelReferenceId: {req.ModelReferenceId}");
        }

        Model? cm = await db.Models.FindAsync([modelId], cancellationToken);
        if (cm == null) return NotFound();

        req.ApplyTo(cm);
        if (db.ChangeTracker.HasChanges())
        {
            cm.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("models")]
    public async Task<ActionResult<int>> CreateModel([FromBody] UpdateModelRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await db.ModelReferences.AnyAsync(r => r.Id == req.ModelReferenceId, cancellationToken))
        {
            return this.BadRequestMessage($"Invalid ModelReferenceId: {req.ModelReferenceId}");
        }

        Model toCreate = new()
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        req.ApplyTo(toCreate);
        db.Models.Add(toCreate);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), toCreate.Id);
    }

    [HttpPost("models/fast-create")]
    public async Task<ActionResult<int>> FastCreateModel([FromBody] ValidateModelRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await db.ModelKeys.AnyAsync(r => r.Id == req.ModelKeyId, cancellationToken))
        {
            return BadRequest($"Invalid ModelKeyId: {req.ModelKeyId}");
        }

        ModelReference? modelRef = await db.ModelReferences
            .Include(x => x.CurrencyCodeNavigation)
            .FirstOrDefaultAsync(x => x.Id == req.ModelReferenceId, cancellationToken);
        if (modelRef == null)
        {
            return BadRequest($"Invalid ModelReferenceId: {req.ModelReferenceId}");
        }

        Model toCreate = new()
        {
            ModelKeyId = req.ModelKeyId,
            ModelReferenceId = req.ModelReferenceId,
            Name = req.DeploymentName ?? modelRef.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeploymentName = req.DeploymentName,
            IsDeleted = false,
            InputTokenPrice1M = modelRef.InputTokenPrice1M * modelRef.CurrencyCodeNavigation.ExchangeRate,
            OutputTokenPrice1M = modelRef.OutputTokenPrice1M * modelRef.CurrencyCodeNavigation.ExchangeRate,
        };
        db.Models.Add(toCreate);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), toCreate.Id);
    }

    [HttpDelete("models/{modelId:int}")]
    public async Task<ActionResult> DeleteModel(short modelId, CancellationToken cancellationToken)
    {
        Model? cm = await db.Models.FindAsync([modelId], cancellationToken);
        if (cm == null) return NotFound();

        var refInfo = await db.Models
            .Where(x => x.Id == modelId)
            .Select(x => new
            {
                ChatConfigs = x.ChatConfigs.Any(),
                UserModels = x.UserModels.Any(),
                ApiKeys = x.ApiKeys.Any(),
                UserApiCache = x.UserApiCaches.Any(),
            })
            .SingleAsync(cancellationToken);

        if (refInfo.ChatConfigs || refInfo.UserModels || refInfo.ApiKeys)
        {
            string message = "Cannot delete model because it is referenced by: ";
            if (refInfo.ChatConfigs) message += "ChatConfigs, ";
            if (refInfo.UserModels) message += "UserModels, ";
            if (refInfo.ApiKeys) message += "ApiKeys, ";
            if (refInfo.UserApiCache) message += "UserApiCache, ";
            return this.BadRequestMessage(message);
        }
        else
        {
            db.Models.Remove(cm);
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }

    [HttpPost("models/validate")]
    public async Task<ActionResult<ModelValidateResult>> ValidateModel(
        [FromBody] ValidateModelRequest req,
        [FromServices] ChatFactory chatFactory,
        CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db.ModelKeys
            .Include(x => x.ModelProvider)
            .Where(x => x.Id == req.ModelKeyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (modelKey == null)
        {
            return this.BadRequestMessage($"Model key id: {req.ModelKeyId} not found");
        }

        ModelReference? modelReference = await db.ModelReferences
            .Include(x => x.Provider)
            .Include(x => x.Tokenizer)
            .Where(x => x.Id == req.ModelReferenceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (modelReference == null)
        {
            return this.BadRequestMessage($"Model reference id: {req.ModelReferenceId} not found");
        }

        ModelValidateResult result = await chatFactory.ValidateModel(modelKey, modelReference, req.DeploymentName, cancellationToken);
        return Ok(result);
    }

    [HttpGet("user-models/{userId:int}")]
    public async Task<ActionResult<UserModelDto[]>> GetUserModels(int userId, CancellationToken cancellationToken)
    {
        UserModelDto[] userModels = await db.Models
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Order)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                Model = x,
                UserModel = x.UserModels.Where(x => x.UserId == userId).FirstOrDefault()
            })
            .Select(x => x.UserModel == null ?
                new UserModelDto()
                {
                    Id = -1,
                    ModelId = x.Model.Id,
                    DisplayName = x.Model.Name,
                    ModelKeyName = x.Model.ModelKey.Name,
                    Enabled = false,
                    Expires = DateTime.UtcNow,
                    Counts = 0,
                    Tokens = 0,
                } : new UserModelDto()
                {
                    Id = x.UserModel.Id,
                    ModelId = x.Model.Id,
                    DisplayName = x.Model.Name,
                    ModelKeyName = x.Model.ModelKey.Name,
                    Counts = x.UserModel.CountBalance,
                    Expires = x.UserModel.ExpiresAt,
                    Enabled = true,
                    Tokens = x.UserModel.TokenBalance,
                })
            .ToArrayAsync(cancellationToken);

        return Ok(userModels);
    }

    [HttpPut("user-models")]
    public async Task<ActionResult> UpdateUserModels([FromBody] UpdateUserModelRequest updateReq,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return this.BadRequestMessage(string.Join("\n", ModelState
                .Skip(1)
                .Where(x => x.Value != null && x.Value.ValidationState == ModelValidationState.Invalid)
                .Select(x => $"{x.Key}: " + string.Join(",", x.Value!.Errors.Select(x => x.ErrorMessage)))));
        }

        HashSet<int> incomingModelIds = [.. updateReq.Models
            .Where(x => x.Id != -1)
            .Select(x => x.Id)];
        Dictionary<short, UserModel> userModels = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .Where(x => x.UserId == updateReq.UserId)
            .ToDictionaryAsync(k => k.ModelId, v => v, cancellationToken);

        // apply changes
        HashSet<UserModel> effectedUserModels = [];
        foreach (JsonTokenBalance req in updateReq.Models)
        {
            if (userModels.TryGetValue(req.ModelId, out UserModel? existingItem))
            {
                // update existing item
                bool hasDifference = req.ApplyTo(existingItem, adminUser.Id, out UsageTransaction? usageTransaction);
                if (usageTransaction != null)
                {
                    db.UsageTransactions.Add(usageTransaction);
                }
                if (hasDifference)
                {
                    effectedUserModels.Add(existingItem);
                }
            }
            else
            {
                // create new, not exists in database but enabled in frontend request
                UserModel newItem = new()
                {
                    UserId = updateReq.UserId,
                    ModelId = req.ModelId,
                    CreatedAt = DateTime.UtcNow,
                };
                req.ApplyTo(newItem, adminUser.Id, out UsageTransaction? usageTransaction);
                if (usageTransaction != null)
                {
                    db.UsageTransactions.Add(usageTransaction);
                }
                userModels[req.ModelId] = newItem;
                db.UserModels.Add(newItem);
                effectedUserModels.Add(newItem);
            }
        }

        // remove items that are not in the request
        foreach (UserModel existingItem in userModels.Values)
        {
            if (!updateReq.Models.Any(x => x.ModelId == existingItem.ModelId))
            {
                db.UserModels.Remove(existingItem);
                if (existingItem.TokenBalance != 0 || existingItem.CountBalance != 0)
                {
                    existingItem.Model.UsageTransactions.Add(new UsageTransaction()
                    {
                        CreditUserId = existingItem.UserId,
                        CreatedAt = DateTime.UtcNow,
                        ModelId = existingItem.ModelId,
                        CountAmount = -existingItem.CountBalance,
                        TokenAmount = -existingItem.TokenBalance,
                        TransactionTypeId = (byte)DBTransactionType.Charge,
                    });
                }
                effectedUserModels.Add(existingItem);
            }
        }

        if (effectedUserModels.Count != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            await balanceService.AsyncUpdateUsage(effectedUserModels.Select(x => x.Id), CancellationToken.None);
            await db.Users
                .Where(x => x.Id == updateReq.UserId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);
        }

        return NoContent();
    }

    [HttpPut("models/reorder")]
    public async Task<ActionResult> ReorderModels([FromBody] ReorderRequest<short> request, CancellationToken cancellationToken)
    {
        // 验证被移动的 Model 是否存在
        Model? sourceModel = await db.Models
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
                .FirstOrDefaultAsync(x => x.Id == request.PreviousId, cancellationToken);
            if (previousModel == null)
            {
                return NotFound("Previous model not found");
            }
            
            // 验证 previous 与 source 属于同一个 ModelKey
            if (previousModel.ModelKeyId != sourceModel.ModelKeyId)
            {
                return BadRequest("Previous model must belong to the same ModelKey");
            }
        }

        if (request.NextId != null)
        {
            nextModel = await db.Models
                .FirstOrDefaultAsync(x => x.Id == request.NextId, cancellationToken);
            if (nextModel == null)
            {
                return NotFound("Next model not found");
            }
            
            // 验证 next 与 source 属于同一个 ModelKey
            if (nextModel.ModelKeyId != sourceModel.ModelKeyId)
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
        if (previousModel != null && nextModel != null && previousModel.Order >= nextModel.Order)
        {
            return BadRequest("Invalid order: previous model should have smaller order than next model");
        }

        // 尝试应用移动
        bool needReorder = !TryApplyMove(sourceModel, previousModel, nextModel);
        
        if (needReorder)
        {
            // 需要重新排序，但只重排序同一个 ModelKey 下的 Models
            Model[] modelsInSameKey = await db.Models
                .Where(x => x.ModelKeyId == sourceModel.ModelKeyId)
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
