using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Chats.Models.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Models;

[Route("api/models"), Authorize]
public class ModelsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminModelDto[]>> Get([FromServices] ChatsDB db, [FromServices] CurrentUser currentUser, CancellationToken cancellationToken)
    {
        AdminModelDto[] data = await (
            from um in db.UserModels
            where um.UserId == currentUser.Id && !um.Model.IsDeleted
            join mpo in db.ModelProviderOrders on um.Model.ModelKey.ModelProviderId equals mpo.ModelProviderId into mpoGroup
            from mpo in mpoGroup.DefaultIfEmpty()
            orderby mpo != null ? mpo.Order : int.MaxValue, um.Model.ModelKey.Order, um.Model.Order
            select um.Model
        )
        .Select(x => new AdminModelDto
            {
                ModelId = x.Id,
                Name = x.Name,
                Enabled = !x.IsDeleted,
                ModelKeyId = x.ModelKeyId,
                ModelProviderId = x.ModelKey.ModelProviderId,
                InputTokenPrice1M = x.InputTokenPrice1M,
                OutputTokenPrice1M = x.OutputTokenPrice1M,
                DeploymentName = x.DeploymentName,
                AllowSearch = x.AllowSearch,
                AllowVision = x.AllowVision,
                AllowStreaming = x.AllowStreaming,
                AllowCodeExecution = x.AllowCodeExecution,
                ReasoningEffortOptions = Model.GetReasoningEffortOptionsAsInt32(x.ReasoningEffortOptions),
                MinTemperature = x.MinTemperature,
                MaxTemperature = x.MaxTemperature,
                ContextWindow = x.ContextWindow,
                MaxResponseTokens = x.MaxResponseTokens,
                AllowToolCall = x.AllowToolCall,
                SupportedImageSizes = Model.GetSupportedImageSizesAsArray(x.SupportedImageSizes),
                ApiType = (DBApiType)x.ApiType,
                UseAsyncApi = x.UseAsyncApi,
                UseMaxCompletionTokens = x.UseMaxCompletionTokens,
                IsLegacy = x.IsLegacy,
                ThinkTagParserEnabled = x.ThinkTagParserEnabled,
                MaxThinkingBudget = x.MaxThinkingBudget
        })
            .ToArrayAsync(cancellationToken);
        return data;
    }

    [HttpGet("{modelId}/usage")]
    public async Task<ActionResult<ModelUsageDto>> GetUsage(short modelId, [FromServices] CurrentUser currentUser, [FromServices] UserModelManager userModelManager, CancellationToken cancellationToken)
    {
        UserModel? model = await userModelManager.GetUserModel(currentUser.Id, modelId, cancellationToken);
        if (model == null) return NotFound();

        ModelUsageDto response = ModelUsageDto.FromDB(model);
        return Ok(response);
    }
}
