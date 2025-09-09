using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.Controllers.Chats.Models.Dtos;
using Chats.BE.DB;
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
        int? fileServiceId = await FileService.GetDefaultId(db, cancellationToken);
        AdminModelDto[] data = await db.UserModels
            .Where(x => x.UserId == currentUser.Id && !x.Model.IsDeleted)
            .OrderBy(x => x.Model.ModelKey.Order).ThenBy(x => x.Model.Order)
            .Select(x => x.Model)
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

    [HttpGet("{modelId}/usage")]
    public async Task<ActionResult<ModelUsageDto>> GetUsage(short modelId, [FromServices] CurrentUser currentUser, [FromServices] UserModelManager userModelManager, CancellationToken cancellationToken)
    {
        UserModel? model = await userModelManager.GetUserModel(currentUser.Id, modelId, cancellationToken);
        if (model == null) return NotFound();

        ModelUsageDto response = ModelUsageDto.FromDB(model);
        return Ok(response);
    }
}
