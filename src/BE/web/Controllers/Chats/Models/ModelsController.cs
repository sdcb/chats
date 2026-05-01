using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Chats.Models.Dtos;
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
            where um.UserId == currentUser.Id && um.Model.Enabled
            join mpo in db.ModelProviderOrders on um.Model.CurrentSnapshot.ModelKeySnapshot.ModelProviderId equals mpo.ModelProviderId into mpoGroup
            from mpo in mpoGroup.DefaultIfEmpty()
            orderby mpo != null ? mpo.Order : int.MaxValue, um.Model.CurrentSnapshot.ModelKeySnapshot.ModelKey!.Order, um.Model.Order
            select um.Model
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

        if (EtagCacheHelper.TryHandleNotModified(this, "models-list", data))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

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
