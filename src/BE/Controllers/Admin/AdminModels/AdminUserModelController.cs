using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.AdminModels;

[Route("api/admin/user-models"), AuthorizeAdmin]
public class AdminUserModelController(ChatsDB db) : ControllerBase
{
    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<UserModelDto[]>> GetUserModels(int userId, CancellationToken cancellationToken)
    {
        UserModelDto[] userModels = await db.UserModels
            .Where(x => x.UserId == userId)
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
            .Include(x => x.Model.ModelKey.ModelProvider)
            .OrderByDescending(x => x.Id)
            .Select(x => new UserModelDto()
            {
                Id = x.Id,
                ModelId = x.Model.Id,
                DisplayName = x.Model.Name,
                ModelKeyName = x.Model.ModelKey.Name,
                ModelProviderId = x.Model.ModelKey.ModelProviderId,
                Counts = x.CountBalance,
                Expires = x.ExpiresAt,
                Tokens = x.TokenBalance,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(userModels);
    }

    [HttpGet("user/{userId:int}/unassigned")]
    public async Task<ActionResult<AdminModelDto[]>> GetUserUnassignedModels(int userId, CancellationToken cancellationToken)
    {
        // 获取用户已分配的模型ID列表
        var assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        // 获取未分配给用户的模型
        int? fileServiceId = await FileService.GetDefaultId(db, cancellationToken);
        var unassignedModels = await db.Models
            .Where(x => !x.IsDeleted && !assignedModelIds.Contains(x.Id))
            .Include(x => x.ModelKey)
            .Include(x => x.ModelReference)
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

        return Ok(unassignedModels);
    }

    [HttpPost]
    public async Task<ActionResult> AddUserModel([FromBody] AddUserModelRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 检查用户是否存在
        bool userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return this.BadRequestMessage($"User with ID {request.UserId} not found");
        }

        // 检查模型是否存在
        bool modelExists = await db.Models.AnyAsync(m => m.Id == request.ModelId, cancellationToken);
        if (!modelExists)
        {
            return this.BadRequestMessage($"Model with ID {request.ModelId} not found");
        }

        // 检查是否已经存在该用户模型
        bool userModelExists = await db.UserModels
            .AnyAsync(um => um.UserId == request.UserId && um.ModelId == request.ModelId, cancellationToken);
        if (userModelExists)
        {
            return this.BadRequestMessage("User model already exists");
        }

        UserModel newUserModel = new()
        {
            UserId = request.UserId,
            ModelId = request.ModelId,
            TokenBalance = request.Tokens,
            CountBalance = request.Counts,
            ExpiresAt = request.Expires,
            CreatedAt = DateTime.UtcNow,
        };

        db.UserModels.Add(newUserModel);

        // 创建使用记录
        if (request.Tokens > 0 || request.Counts > 0)
        {
            db.UsageTransactions.Add(new UsageTransaction()
            {
                CreditUserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
                ModelId = request.ModelId,
                CountAmount = request.Counts,
                TokenAmount = request.Tokens,
                TransactionTypeId = (byte)DBTransactionType.Charge,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await balanceService.AsyncUpdateUsage([newUserModel.Id], CancellationToken.None);
        await db.Users
            .Where(x => x.Id == request.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);

        return Ok();
    }

    [HttpPut("{userModelId:int}")]
    public async Task<ActionResult> EditUserModel(int userModelId, [FromBody] EditUserModelRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserModel? userModel = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .FirstOrDefaultAsync(um => um.Id == userModelId, cancellationToken);
        if (userModel == null)
        {
            return NotFound("User model not found");
        }

        bool needsTransaction = userModel.CountBalance != request.Counts || userModel.TokenBalance != request.Tokens;
        bool hasDifference = needsTransaction || userModel.ExpiresAt != request.Expires;

        if (needsTransaction)
        {
            db.UsageTransactions.Add(new UsageTransaction()
            {
                CreditUserId = userModel.UserId,
                CreatedAt = DateTime.UtcNow,
                CountAmount = request.Counts - userModel.CountBalance,
                TokenAmount = request.Tokens - userModel.TokenBalance,
                ModelId = userModel.ModelId,
                TransactionTypeId = (byte)DBTransactionType.Charge,
            });
        }

        if (hasDifference)
        {
            userModel.CountBalance = request.Counts;
            userModel.TokenBalance = request.Tokens;
            userModel.ExpiresAt = request.Expires;

            await db.SaveChangesAsync(cancellationToken);
            await balanceService.AsyncUpdateUsage([userModel.Id], CancellationToken.None);
            await db.Users
                .Where(x => x.Id == userModel.UserId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);
        }

        return NoContent();
    }

    [HttpDelete("{userModelId:int}")]
    public async Task<ActionResult> DeleteUserModel(int userModelId,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        UserModel? userModel = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .FirstOrDefaultAsync(um => um.Id == userModelId, cancellationToken);
        if (userModel == null)
        {
            return NotFound("User model not found");
        }

        db.UserModels.Remove(userModel);

        // 如果有余额，需要创建退款记录
        if (userModel.TokenBalance != 0 || userModel.CountBalance != 0)
        {
            userModel.Model.UsageTransactions.Add(new UsageTransaction()
            {
                CreditUserId = userModel.UserId,
                CreatedAt = DateTime.UtcNow,
                ModelId = userModel.ModelId,
                CountAmount = -userModel.CountBalance,
                TokenAmount = -userModel.TokenBalance,
                TransactionTypeId = (byte)DBTransactionType.Charge,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await balanceService.AsyncUpdateUsage([userModel.Id], CancellationToken.None);
        await db.Users
            .Where(x => x.Id == userModel.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);

        return NoContent();
    }
}
