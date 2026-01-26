using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.AdminModels;

[Route("api/admin/user-models"), AuthorizeAdmin]
public class AdminUserModelController(ChatsDB db) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<UserModelPermissionUserDto>>> GetUsersForPermission(QueryPagingRequest pagingRequest, CancellationToken cancellationToken)
    {
        IQueryable<User> query = db.Users
            .OrderByDescending(x => x.UpdatedAt);

        int totalModelProviderCount = await db.ModelProviderOrders
            .CountAsync(cancellationToken);

        if (totalModelProviderCount == 0)
        {
            totalModelProviderCount = await db.ModelKeys
                .Select(x => x.ModelProviderId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        if (!string.IsNullOrEmpty(pagingRequest.Query))
        {
            query = query.Where(x => x.UserName.Contains(pagingRequest.Query) 
                                  || (x.Email != null && x.Email.Contains(pagingRequest.Query))
                                  || (x.Phone != null && x.Phone.Contains(pagingRequest.Query)));
        }

        PagedResult<UserModelPermissionUserDto> result = await PagedResult.FromQuery(
            query.Select(x => new UserModelPermissionUserDto
            {
                Id = x.Id,
                Username = x.UserName,
                Email = x.Email,
                Phone = x.Phone,
                Enabled = x.Enabled,
                UserModelCount = x.UserModels.Count(um => !um.Model.IsDeleted),
                ModelProviderCount = totalModelProviderCount,
            }),
            pagingRequest,
            cancellationToken
        );

        return Ok(result);
    }

    [HttpGet("user/{userId:int}/providers")]
    public async Task<ActionResult<UserModelProviderDto[]>> GetModelProvidersForUser(int userId, CancellationToken cancellationToken)
    {
        // 获取用户已分配的模型ID
        List<short> assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        // 获取所有提供商的统计信息
        UserModelProviderDto[] providers = await (
            from mk in db.ModelKeys
            join m in db.Models on mk.Id equals m.ModelKeyId into models
            from m in models.DefaultIfEmpty()
            where m == null || !m.IsDeleted
            group new { mk, m } by mk.ModelProviderId into g
            orderby g.Key
            select new UserModelProviderDto
            {
                ProviderId = g.Key,
                KeyCount = g.Select(x => x.mk.Id).Distinct().Count(),
                ModelCount = g.Sum(x => x.m != null ? 1 : 0),
                AssignedModelCount = g.Sum(x => x.m != null && assignedModelIds.Contains(x.m.Id) ? 1 : 0)
            }
        ).ToArrayAsync(cancellationToken);

        return Ok(providers);
    }

    [HttpGet("user/{userId:int}/provider/{providerId:int}/keys")]
    public async Task<ActionResult<UserModelKeyDto[]>> GetModelKeysByProviderForUser(int userId, int providerId, CancellationToken cancellationToken)
    {
        // 获取用户已分配的模型ID
        List<short> assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        // 获取该提供商下所有密钥的统计信息
        UserModelKeyDto[] keys = await (
            from mk in db.ModelKeys
            where mk.ModelProviderId == providerId
            join m in db.Models on mk.Id equals m.ModelKeyId into models
            from m in models.DefaultIfEmpty()
            where m == null || !m.IsDeleted
            group new { mk, m } by new { mk.Id, mk.Name, mk.Order } into g
            orderby g.Key.Order
            select new UserModelKeyDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                ModelCount = g.Sum(x => x.m != null ? 1 : 0),
                AssignedModelCount = g.Sum(x => x.m != null && assignedModelIds.Contains(x.m.Id) ? 1 : 0)
            }
        ).ToArrayAsync(cancellationToken);

        return Ok(keys);
    }

    [HttpGet("user/{userId:int}/key/{keyId:int}/models")]
    public async Task<ActionResult<UserModelPermissionModelDto[]>> GetModelsByKeyForUser(int userId, int keyId, CancellationToken cancellationToken)
    {
        // 获取用户已分配的模型及其详细信息
        var userModels = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => new { 
                x.ModelId, 
                x.Id, 
                x.CountBalance, 
                x.TokenBalance, 
                x.ExpiresAt 
            })
            .ToListAsync(cancellationToken);

        var userModelDict = userModels.ToDictionary(
            x => x.ModelId, 
            x => new { x.Id, x.CountBalance, x.TokenBalance, x.ExpiresAt }
        );

        // 获取该密钥下的所有模型
        var models = await db.Models
            .Where(x => x.ModelKeyId == keyId)
            .OrderBy(x => x.Order)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Order,
                x.IsDeleted
            })
            .ToArrayAsync(cancellationToken);

        UserModelPermissionModelDto[] result = models
            .Select(x =>
            {
                bool isAssigned = userModelDict.ContainsKey(x.Id);
                var userModelInfo = isAssigned ? userModelDict[x.Id] : null;

                return new UserModelPermissionModelDto
                {
                    ModelId = x.Id,
                    Name = x.Name,
                    IsAssigned = isAssigned,
                    UserModelId = userModelInfo?.Id,
                    Counts = userModelInfo?.CountBalance,
                    Tokens = userModelInfo?.TokenBalance,
                    Expires = userModelInfo?.ExpiresAt,
                    IsDeleted = x.IsDeleted
                };
            })
            // 过滤：如果用户没有权限且模型已被禁用（IsDeleted=true），则不显示
            .Where(x => x.IsAssigned || !x.IsDeleted)
            .ToArray();

        return Ok(result);
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<UserModelDto[]>> GetUserModels(int userId, CancellationToken cancellationToken)
    {
        UserModelDto[] userModels = await db.UserModels
            .Where(x => x.UserId == userId)
            .Include(x => x.Model)
            .Include(x => x.Model.ModelKey)
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

    [HttpGet("model/{modelId:int}")]
    public async Task<ActionResult<UserModelUserDto[]>> GetUsersByModel(int modelId, CancellationToken cancellationToken)
    {
        UserModelUserDto[] users = await db.UserModels
            .Where(x => x.ModelId == modelId)
            .Include(x => x.User)
            .OrderByDescending(x => x.Id)
            .Select(x => new UserModelUserDto()
            {
                Id = x.Id,
                UserId = x.UserId,
                Username = x.User.UserName,
                DisplayName = x.User.DisplayName,
                Counts = x.CountBalance,
                Expires = x.ExpiresAt,
                Tokens = x.TokenBalance,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("user/{userId:int}/unassigned")]
    public async Task<ActionResult<AdminModelDto[]>> GetUserUnassignedModels(int userId, CancellationToken cancellationToken)
    {
        // 获取用户已分配的模型ID列表
        List<short> assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        // 获取未分配给用户的模型
        AdminModelDto[] unassignedModels = await (
            from m in db.Models
            where !m.IsDeleted && !assignedModelIds.Contains(m.Id)
            join mpo in db.ModelProviderOrders on m.ModelKey.ModelProviderId equals mpo.ModelProviderId into mpoGroup
            from mpo in mpoGroup.DefaultIfEmpty()
            orderby mpo != null ? mpo.Order : int.MaxValue, m.ModelKey.Order, m.Order
            select new AdminModelDto
            {
                ModelId = m.Id,
                Name = m.Name,
                Enabled = !m.IsDeleted,
                ModelKeyId = m.ModelKeyId,
                ModelProviderId = m.ModelKey.ModelProviderId,
                InputFreshTokenPrice1M = m.InputFreshTokenPrice1M,
                OutputTokenPrice1M = m.OutputTokenPrice1M,
                InputCachedTokenPrice1M = m.InputCachedTokenPrice1M,
                DeploymentName = m.DeploymentName,
                AllowSearch = m.AllowSearch,
                AllowVision = m.AllowVision,
                AllowStreaming = m.AllowStreaming,
                AllowCodeExecution = m.AllowCodeExecution,
                ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(m.ReasoningEffortOptions),
                MinTemperature = m.MinTemperature,
                MaxTemperature = m.MaxTemperature,
                ContextWindow = m.ContextWindow,
                MaxResponseTokens = m.MaxResponseTokens,
                AllowToolCall = m.AllowToolCall,
                SupportedImageSizes = Model.GetSupportedImageSizesAsArray(m.SupportedImageSizes),
                ApiType = (DBApiType)m.ApiTypeId,
                UseAsyncApi = m.UseAsyncApi,
                UseMaxCompletionTokens = m.UseMaxCompletionTokens,
                IsLegacy = m.IsLegacy,
                ThinkTagParserEnabled = m.ThinkTagParserEnabled,
                MaxThinkingBudget = m.MaxThinkingBudget,
                SupportsVisionLink = m.SupportsVisionLink,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(unassignedModels);
    }

    [HttpPost]
    public async Task<ActionResult<UserModelOperationResponse>> AddUserModel([FromBody] AddUserModelRequest request,
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
            return BadRequest($"User with ID {request.UserId} not found");
        }

        // 检查模型是否存在并获取相关信息
        var modelInfo = await db.Models
            .Where(m => m.Id == request.ModelId)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.Order,
                m.IsDeleted,
                m.ModelKeyId,
                ModelKeyName = m.ModelKey.Name,
                ModelProviderId = m.ModelKey.ModelProviderId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (modelInfo == null)
        {
            return BadRequest($"Model with ID {request.ModelId} not found");
        }

        // 检查是否已经存在该用户模型
        bool userModelExists = await db.UserModels
            .AnyAsync(um => um.UserId == request.UserId && um.ModelId == request.ModelId, cancellationToken);
        if (userModelExists)
        {
            return BadRequest("User model already exists");
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

        // 获取更新后的统计信息
        int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
        UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, modelInfo.ModelProviderId, cancellationToken);
        UserModelKeyDto? keyStats = await GetKeyStats(request.UserId, modelInfo.ModelKeyId, cancellationToken);

        UserModelOperationResponse response = new UserModelOperationResponse
        {
            AffectedCount = 1,
            UserModelCount = userModelCount,
            ProviderStats = providerStats,
            KeyStats = keyStats,
            Model = new UserModelPermissionModelDto
            {
                ModelId = modelInfo.Id,
                Name = modelInfo.Name,
                IsAssigned = true,
                UserModelId = newUserModel.Id,
                Counts = request.Counts,
                Tokens = request.Tokens,
                Expires = request.Expires,
                IsDeleted = modelInfo.IsDeleted
            }
        };

        return Ok(response);
    }

    [HttpPut("{userModelId:int}")]
    public async Task<ActionResult<UserModelOperationResponse>> EditUserModel(int userModelId, [FromBody] EditUserModelRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserModel? userModel = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.UsageTransactions)
            .Include(x => x.Model.ModelKey)
            .FirstOrDefaultAsync(um => um.Id == userModelId, cancellationToken);
        if (userModel == null)
        {
            return NotFound("User model not found");
        }

        bool needsTransaction = request.CountsDelta != 0 || request.TokensDelta != 0;
        bool hasDifference = needsTransaction || userModel.ExpiresAt != request.Expires;

        if (needsTransaction)
        {
            db.UsageTransactions.Add(new UsageTransaction()
            {
                CreditUserId = userModel.UserId,
                CreatedAt = DateTime.UtcNow,
                CountAmount = request.CountsDelta,
                TokenAmount = request.TokensDelta,
                ModelId = userModel.ModelId,
                TransactionTypeId = (byte)DBTransactionType.Charge,
            });
        }

        if (hasDifference)
        {
            userModel.CountBalance += request.CountsDelta;
            userModel.TokenBalance += request.TokensDelta;
            userModel.ExpiresAt = request.Expires;

            await db.SaveChangesAsync(cancellationToken);
            await balanceService.AsyncUpdateUsage([userModel.Id], CancellationToken.None);
            await db.Users
                .Where(x => x.Id == userModel.UserId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);
        }

        int userId = userModel.UserId;
        short modelId = userModel.ModelId;
        short modelKeyId = userModel.Model.ModelKeyId;
        short modelProviderId = userModel.Model.ModelKey.ModelProviderId;

        // 获取更新后的统计信息
        int userModelCount = await GetUserModelCount(userId, cancellationToken);
        UserModelProviderDto? providerStats = await GetProviderStats(userId, modelProviderId, cancellationToken);
        UserModelKeyDto? keyStats = await GetKeyStats(userId, modelKeyId, cancellationToken);

        UserModelOperationResponse response = new UserModelOperationResponse
        {
            AffectedCount = hasDifference ? 1 : 0,
            UserModelCount = userModelCount,
            ProviderStats = providerStats,
            KeyStats = keyStats,
            Model = new UserModelPermissionModelDto
            {
                ModelId = modelId,
                Name = userModel.Model.Name,
                IsAssigned = true,
                UserModelId = userModelId,
                Counts = userModel.CountBalance,
                Tokens = userModel.TokenBalance,
                Expires = userModel.ExpiresAt,
                IsDeleted = userModel.Model.IsDeleted
            }
        };

        return Ok(response);
    }

    [HttpDelete("{userModelId:int}")]
    public async Task<ActionResult<UserModelOperationResponse>> DeleteUserModel(int userModelId,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        UserModel? userModel = await db.UserModels
            .Include(x => x.Model)
            .Include(x => x.Model.UsageTransactions)
            .Include(x => x.Model.ModelKey)
            .FirstOrDefaultAsync(um => um.Id == userModelId, cancellationToken);
        if (userModel == null)
        {
            return NotFound("User model not found");
        }

        int userId = userModel.UserId;
        short modelId = userModel.ModelId;
        short modelKeyId = userModel.Model.ModelKeyId;
        short modelProviderId = userModel.Model.ModelKey.ModelProviderId;
        string modelName = userModel.Model.Name;
        bool modelIsDeleted = userModel.Model.IsDeleted;

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
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow), CancellationToken.None);

        // 获取更新后的统计信息
        int userModelCount = await GetUserModelCount(userId, cancellationToken);
        UserModelProviderDto? providerStats = await GetProviderStats(userId, modelProviderId, cancellationToken);
        UserModelKeyDto? keyStats = await GetKeyStats(userId, modelKeyId, cancellationToken);

        UserModelOperationResponse response = new UserModelOperationResponse
        {
            AffectedCount = 1,
            UserModelCount = userModelCount,
            ProviderStats = providerStats,
            KeyStats = keyStats,
            Model = new UserModelPermissionModelDto
            {
                ModelId = modelId,
                Name = modelName,
                IsAssigned = false,
                UserModelId = null,
                Counts = null,
                Tokens = null,
                Expires = null,
                IsDeleted = modelIsDeleted
            }
        };

        return Ok(response);
    }

    // 辅助方法：获取用户模型总数（只统计未删除的模型）
    private async Task<int> GetUserModelCount(int userId, CancellationToken cancellationToken)
    {
        return await db.UserModels
            .Where(x => x.UserId == userId && !x.Model.IsDeleted)
            .CountAsync(cancellationToken);
    }

    // 辅助方法：获取 Provider 统计信息
    private async Task<UserModelProviderDto?> GetProviderStats(int userId, int providerId, CancellationToken cancellationToken)
    {
        List<short> assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        return await (
            from mk in db.ModelKeys
            where mk.ModelProviderId == providerId
            join m in db.Models on mk.Id equals m.ModelKeyId into models
            from m in models.DefaultIfEmpty()
            where m == null || !m.IsDeleted
            group new { mk, m } by mk.ModelProviderId into g
            select new UserModelProviderDto
            {
                ProviderId = g.Key,
                KeyCount = g.Select(x => x.mk.Id).Distinct().Count(),
                ModelCount = g.Sum(x => x.m != null ? 1 : 0),
                AssignedModelCount = g.Sum(x => x.m != null && assignedModelIds.Contains(x.m.Id) ? 1 : 0)
            }
        ).FirstOrDefaultAsync(cancellationToken);
    }

    // 辅助方法：获取 Key 统计信息
    private async Task<UserModelKeyDto?> GetKeyStats(int userId, int keyId, CancellationToken cancellationToken)
    {
        List<short> assignedModelIds = await db.UserModels
            .Where(x => x.UserId == userId)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken);

        return await (
            from mk in db.ModelKeys
            where mk.Id == keyId
            join m in db.Models on mk.Id equals m.ModelKeyId into models
            from m in models.DefaultIfEmpty()
            where m == null || !m.IsDeleted
            group new { mk, m } by new { mk.Id, mk.Name, mk.Order } into g
            select new UserModelKeyDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                ModelCount = g.Sum(x => x.m != null ? 1 : 0),
                AssignedModelCount = g.Sum(x => x.m != null && assignedModelIds.Contains(x.m.Id) ? 1 : 0)
            }
        ).FirstOrDefaultAsync(cancellationToken);
    }

    [HttpPost("batch-by-provider")]
    public async Task<ActionResult<UserModelOperationResponse>> BatchAddUserModelsByProvider([FromBody] BatchUserModelsByProviderRequest request,
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
            return BadRequest($"User with ID {request.UserId} not found");
        }

        // 获取该Provider下所有模型ID
        List<short> modelIds = await db.Models
            .Where(m => m.ModelKey.ModelProviderId == request.ProviderId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (modelIds.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = null,
                Model = null
            });
        }

        // 获取用户已有的模型
        List<short> existingUserModelIds = await db.UserModels
            .Where(um => um.UserId == request.UserId && modelIds.Contains(um.ModelId))
            .Select(um => um.ModelId)
            .ToListAsync(cancellationToken);

        // 过滤出需要添加的新模型
        List<short> modelIdsToAdd = modelIds.Except(existingUserModelIds).ToList();

        if (modelIdsToAdd.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = null,
                Model = null
            });
        }

        List<UserModel> newUserModels = new List<UserModel>();
        DateTime createdTime = DateTime.UtcNow;
        DateTime defaultExpiresAt = createdTime.AddYears(1);

        foreach (short modelId in modelIdsToAdd)
        {
            UserModel userModel = new UserModel
            {
                UserId = request.UserId,
                ModelId = (short)modelId,
                TokenBalance = 0,
                CountBalance = 0,
                ExpiresAt = defaultExpiresAt,
                CreatedAt = createdTime,
            };
            newUserModels.Add(userModel);
            db.UserModels.Add(userModel);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 获取更新后的统计信息
        int updatedUserModelCount = await GetUserModelCount(request.UserId, cancellationToken);
        UserModelProviderDto? updatedProviderStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);

        return Ok(new UserModelOperationResponse
        {
            AffectedCount = newUserModels.Count,
            UserModelCount = updatedUserModelCount,
            ProviderStats = updatedProviderStats,
            KeyStats = null,
            Model = null
        });
    }

    [HttpPost("batch-delete-by-provider")]
    public async Task<ActionResult<UserModelOperationResponse>> BatchDeleteUserModelsByProvider([FromBody] BatchUserModelsByProviderRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 获取该Provider下所有未删除的模型ID
        List<short> modelIds = await db.Models
            .Where(m => m.ModelKey.ModelProviderId == request.ProviderId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (modelIds.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = null,
                Model = null
            });
        }

        // 查找需要删除的用户模型
        List<UserModel> userModels = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .Where(um => um.UserId == request.UserId && modelIds.Contains(um.ModelId))
            .ToListAsync(cancellationToken);

        if (userModels.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = null,
                Model = null
            });
        }

        List<int> userModelIds = new List<int>();
        DateTime createdTime = DateTime.UtcNow;

        foreach (UserModel? userModel in userModels)
        {
            userModelIds.Add(userModel.Id);
            db.UserModels.Remove(userModel);

            // 如果有余额，需要创建退款记录
            if (userModel.TokenBalance != 0 || userModel.CountBalance != 0)
            {
                userModel.Model.UsageTransactions.Add(new UsageTransaction
                {
                    CreditUserId = userModel.UserId,
                    CreatedAt = createdTime,
                    ModelId = userModel.ModelId,
                    CountAmount = -userModel.CountBalance,
                    TokenAmount = -userModel.TokenBalance,
                    TransactionTypeId = (byte)DBTransactionType.Charge,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // 获取更新后的统计信息
        int updatedUserModelCount = await GetUserModelCount(request.UserId, cancellationToken);
        UserModelProviderDto? updatedProviderStats = await GetProviderStats(request.UserId, request.ProviderId, cancellationToken);

        return Ok(new UserModelOperationResponse
        {
            AffectedCount = userModels.Count,
            UserModelCount = updatedUserModelCount,
            ProviderStats = updatedProviderStats,
            KeyStats = null,
            Model = null
        });
    }

    [HttpPost("batch-by-key")]
    public async Task<ActionResult<UserModelOperationResponse>> BatchAddUserModelsByKey([FromBody] BatchUserModelsByKeyRequest request,
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
            return BadRequest($"User with ID {request.UserId} not found");
        }

        // 获取 Key 的 ProviderId
        var keyInfo = await db.ModelKeys
            .Where(k => k.Id == request.KeyId)
            .Select(k => new { k.ModelProviderId })
            .FirstOrDefaultAsync(cancellationToken);

        if (keyInfo == null)
        {
            return BadRequest($"Key with ID {request.KeyId} not found");
        }

        // 获取该Key下所有模型ID
        List<short> modelIds = await db.Models
            .Where(m => m.ModelKeyId == request.KeyId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (modelIds.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
            UserModelKeyDto? keyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = keyStats,
                Model = null
            });
        }

        // 获取用户已有的模型
        List<short> existingUserModelIds = await db.UserModels
            .Where(um => um.UserId == request.UserId && modelIds.Contains(um.ModelId))
            .Select(um => um.ModelId)
            .ToListAsync(cancellationToken);

        // 过滤出需要添加的新模型
        List<short> modelIdsToAdd = modelIds.Except(existingUserModelIds).ToList();

        if (modelIdsToAdd.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
            UserModelKeyDto? keyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = keyStats,
                Model = null
            });
        }

        List<UserModel> newUserModels = new List<UserModel>();
        DateTime createdTime = DateTime.UtcNow;
        DateTime defaultExpiresAt = createdTime.AddYears(1);

        foreach (short modelId in modelIdsToAdd)
        {
            UserModel userModel = new UserModel
            {
                UserId = request.UserId,
                ModelId = (short)modelId,
                TokenBalance = 0,
                CountBalance = 0,
                ExpiresAt = defaultExpiresAt,
                CreatedAt = createdTime,
            };
            newUserModels.Add(userModel);
            db.UserModels.Add(userModel);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 获取更新后的统计信息
        int updatedUserModelCount = await GetUserModelCount(request.UserId, cancellationToken);
        UserModelProviderDto? updatedProviderStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
        UserModelKeyDto? updatedKeyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);

        return Ok(new UserModelOperationResponse
        {
            AffectedCount = newUserModels.Count,
            UserModelCount = updatedUserModelCount,
            ProviderStats = updatedProviderStats,
            KeyStats = updatedKeyStats,
            Model = null
        });
    }

    [HttpPost("batch-delete-by-key")]
    public async Task<ActionResult<UserModelOperationResponse>> BatchDeleteUserModelsByKey([FromBody] BatchUserModelsByKeyRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 获取 Key 的 ProviderId
        var keyInfo = await db.ModelKeys
            .Where(k => k.Id == request.KeyId)
            .Select(k => new { k.ModelProviderId })
            .FirstOrDefaultAsync(cancellationToken);

        if (keyInfo == null)
        {
            return BadRequest($"Key with ID {request.KeyId} not found");
        }

        // 获取该Key下所有未删除的模型ID
        List<short> modelIds = await db.Models
            .Where(m => m.ModelKeyId == request.KeyId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (modelIds.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
            UserModelKeyDto? keyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = keyStats,
                Model = null
            });
        }

        // 查找需要删除的用户模型
        List<UserModel> userModels = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .Where(um => um.UserId == request.UserId && modelIds.Contains(um.ModelId))
            .ToListAsync(cancellationToken);

        if (userModels.Count == 0)
        {
            int userModelCount = await GetUserModelCount(request.UserId, cancellationToken);
            UserModelProviderDto? providerStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
            UserModelKeyDto? keyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);
            return Ok(new UserModelOperationResponse
            {
                AffectedCount = 0,
                UserModelCount = userModelCount,
                ProviderStats = providerStats,
                KeyStats = keyStats,
                Model = null
            });
        }

        List<int> userModelIds = new List<int>();
        DateTime createdTime = DateTime.UtcNow;

        foreach (UserModel? userModel in userModels)
        {
            userModelIds.Add(userModel.Id);
            db.UserModels.Remove(userModel);

            // 如果有余额，需要创建退款记录
            if (userModel.TokenBalance != 0 || userModel.CountBalance != 0)
            {
                userModel.Model.UsageTransactions.Add(new UsageTransaction
                {
                    CreditUserId = userModel.UserId,
                    CreatedAt = createdTime,
                    ModelId = userModel.ModelId,
                    CountAmount = -userModel.CountBalance,
                    TokenAmount = -userModel.TokenBalance,
                    TransactionTypeId = (byte)DBTransactionType.Charge,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // 获取更新后的统计信息
        int updatedUserModelCount = await GetUserModelCount(request.UserId, cancellationToken);
        UserModelProviderDto? updatedProviderStats = await GetProviderStats(request.UserId, keyInfo.ModelProviderId, cancellationToken);
        UserModelKeyDto? updatedKeyStats = await GetKeyStats(request.UserId, request.KeyId, cancellationToken);

        return Ok(new UserModelOperationResponse
        {
            AffectedCount = userModels.Count,
            UserModelCount = updatedUserModelCount,
            ProviderStats = updatedProviderStats,
            KeyStats = updatedKeyStats,
            Model = null
        });
    }

    [HttpGet("model/{modelId:int}/users")]
    public async Task<ActionResult<PagedResult<ModelUserPermissionDto>>> GetUsersByModel(int modelId, QueryPagingRequest pagingRequest, CancellationToken cancellationToken)
    {
        // 检查模型是否存在
        bool modelExists = await db.Models.AnyAsync(m => m.Id == modelId, cancellationToken);
        if (!modelExists)
        {
            return NotFound($"Model with ID {modelId} not found");
        }

        // 获取该模型的用户模型映射（用于快速查询已分配状态）
        // 如果同一个用户有多个记录，只取最新的一个（按ID降序）
        var userModelMappings = await db.UserModels
            .Where(um => um.ModelId == modelId)
            .Select(um => new
            {
                um.UserId,
                um.Id,
                um.CountBalance,
                um.TokenBalance,
                um.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        var userModelDict = userModelMappings
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Id).First()
            )
            .ToDictionary(
                x => x.Key,
                x => new { x.Value.Id, x.Value.CountBalance, x.Value.TokenBalance, x.Value.ExpiresAt }
            );

        // 获取已分配该模型的用户ID列表
        HashSet<int> assignedUserIds = userModelDict.Keys.ToHashSet();

        // 构建用户查询：只返回启用的用户，或者已分配该模型的用户（即使被禁用）
        IQueryable<User> query = db.Users
            .Where(x => x.Enabled || assignedUserIds.Contains(x.Id))
            .OrderByDescending(x => x.UpdatedAt);

        if (!string.IsNullOrEmpty(pagingRequest.Query))
        {
            query = query.Where(x => x.UserName.Contains(pagingRequest.Query)
                                  || (x.Email != null && x.Email.Contains(pagingRequest.Query))
                                  || (x.Phone != null && x.Phone.Contains(pagingRequest.Query)));
        }

        PagedResult<ModelUserPermissionDto> result = await PagedResult.FromQuery(
            query.Select(x => new ModelUserPermissionDto
            {
                UserId = x.Id,
                Username = x.UserName,
                Email = x.Email,
                Phone = x.Phone,
                Enabled = x.Enabled,
                IsAssigned = userModelDict.ContainsKey(x.Id),
                UserModelId = userModelDict.ContainsKey(x.Id) ? userModelDict[x.Id].Id : null,
                Counts = userModelDict.ContainsKey(x.Id) ? userModelDict[x.Id].CountBalance : null,
                Tokens = userModelDict.ContainsKey(x.Id) ? userModelDict[x.Id].TokenBalance : null,
                Expires = userModelDict.ContainsKey(x.Id) ? userModelDict[x.Id].ExpiresAt : null
            }),
            pagingRequest,
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("batch-by-model")]
    public async Task<ActionResult> BatchAddUserModelsByModel([FromBody] BatchUserModelsByModelRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 检查模型是否存在并获取相关信息
        var modelInfo = await db.Models
            .Where(m => m.Id == request.ModelId)
            .Select(m => new
            {
                m.Id,
                m.IsDeleted,
                m.ModelKeyId,
                ModelProviderId = m.ModelKey.ModelProviderId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (modelInfo == null)
        {
            return BadRequest($"Model with ID {request.ModelId} not found");
        }

        if (modelInfo.IsDeleted)
        {
            return BadRequest("Cannot add a deleted model to users");
        }

        // 验证所有用户ID是否存在
        List<int> validUserIds = await db.Users
            .Where(u => request.UserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (validUserIds.Count == 0)
        {
            return Ok();
        }

        // 获取已经分配了该模型的用户ID
        List<int> existingUserIds = await db.UserModels
            .Where(um => um.ModelId == request.ModelId && validUserIds.Contains(um.UserId))
            .Select(um => um.UserId)
            .ToListAsync(cancellationToken);

        // 过滤出需要添加的新用户
        List<int> userIdsToAdd = validUserIds.Except(existingUserIds).ToList();

        if (userIdsToAdd.Count == 0)
        {
            return Ok();
        }

        List<UserModel> newUserModels = new List<UserModel>();
        DateTime createdTime = DateTime.UtcNow;
        DateTime defaultExpiresAt = createdTime.AddYears(1);

        foreach (int userId in userIdsToAdd)
        {
            UserModel userModel = new UserModel
            {
                UserId = userId,
                ModelId = (short)request.ModelId,
                TokenBalance = 0,
                CountBalance = 0,
                ExpiresAt = defaultExpiresAt,
                CreatedAt = createdTime,
            };
            newUserModels.Add(userModel);
            db.UserModels.Add(userModel);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpPost("batch-delete-by-model")]
    public async Task<ActionResult> BatchDeleteUserModelsByModel([FromBody] BatchUserModelsByModelRequest request,
        [FromServices] BalanceService balanceService,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 检查模型是否存在
        bool modelExists = await db.Models.AnyAsync(m => m.Id == request.ModelId, cancellationToken);
        if (!modelExists)
        {
            return BadRequest($"Model with ID {request.ModelId} not found");
        }

        // 查找需要删除的用户模型（仅删除指定的用户ID）
        List<UserModel> userModels = await db.UserModels
            .Include(x => x.Model.UsageTransactions)
            .Where(um => um.ModelId == request.ModelId && request.UserIds.Contains(um.UserId))
            .ToListAsync(cancellationToken);

        if (userModels.Count == 0)
        {
            return Ok();
        }

        DateTime createdTime = DateTime.UtcNow;

        foreach (UserModel? userModel in userModels)
        {
            db.UserModels.Remove(userModel);

            // 如果有余额，需要创建退款记录
            if (userModel.TokenBalance != 0 || userModel.CountBalance != 0)
            {
                userModel.Model.UsageTransactions.Add(new UsageTransaction
                {
                    CreditUserId = userModel.UserId,
                    CreatedAt = createdTime,
                    ModelId = userModel.ModelId,
                    CountAmount = -userModel.CountBalance,
                    TokenAmount = -userModel.TokenBalance,
                    TransactionTypeId = (byte)DBTransactionType.Charge,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}
