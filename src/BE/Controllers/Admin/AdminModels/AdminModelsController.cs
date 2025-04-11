using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
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
            .OrderBy(x => x.Order)
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
                Rank = x.Order,
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
            })
            .SingleAsync(cancellationToken);

        if (refInfo.ChatConfigs || refInfo.UserModels || refInfo.ApiKeys)
        {
            string message = "Cannot delete model because it is referenced by: ";
            if (refInfo.ChatConfigs) message += "ChatConfigs, ";
            if (refInfo.UserModels) message += "UserModels, ";
            if (refInfo.ApiKeys) message += "ApiKeys, ";
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
                    Enabled = !x.UserModel.IsDeleted,
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

        HashSet<int> userModelIds = updateReq.Models
            .Where(x => x.Id != -1)
            .Select(x => x.Id)
            .ToHashSet();
        Dictionary<short, UserModel> userModels = await db.UserModels
            .Where(x => x.UserId == updateReq.UserId && userModelIds.Contains(x.Id))
            .ToDictionaryAsync(k => k.ModelId, v => v, cancellationToken);

        // apply changes
        HashSet<UserModel> effectedUserModels = [];
        foreach (JsonTokenBalance req in updateReq.Models)
        {
            if (userModels.TryGetValue(req.ModelId, out UserModel? existingItem))
            {
                bool hasDifference = req.ApplyTo(existingItem, adminUser.Id);
                if (hasDifference)
                {
                    effectedUserModels.Add(existingItem);
                }
            }
            else if (req.Enabled) // not exists in database but enabled in frontend request
            {
                UserModel newItem = new()
                {
                    UserId = updateReq.UserId,
                    ModelId = req.ModelId,
                    CreatedAt = DateTime.UtcNow,
                };
                req.ApplyTo(newItem, adminUser.Id);
                userModels[req.ModelId] = newItem;
                db.UserModels.Add(newItem);
                effectedUserModels.Add(newItem);
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
}
