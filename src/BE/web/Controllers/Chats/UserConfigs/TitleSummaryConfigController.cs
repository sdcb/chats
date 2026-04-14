using Chats.BE.Controllers.Chats.UserConfigs.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.TitleSummary;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.UserConfigs;

[Route("api/user-configs/title-summary"), Authorize]
public sealed class TitleSummaryConfigController(
    ChatsDB db,
    CurrentUser currentUser,
    UserModelManager userModelManager,
    TitleSummaryConfigService configService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TitleSummarySettingsDto>> Get(CancellationToken cancellationToken)
    {
        TitleSummaryConfig? adminConfig = await configService.GetAdminConfig(cancellationToken);
        TitleSummaryConfig? userConfig = await configService.GetUserConfig(currentUser.Id, cancellationToken);

        return Ok(new TitleSummarySettingsDto
        {
            AdminConfig = adminConfig,
            UserConfig = userConfig,
            ResolvedConfig = configService.Resolve(adminConfig, userConfig),
        });
    }

    [HttpGet("default-template")]
    public ActionResult<TitleSummaryDefaultTemplateDto> GetDefaultTemplate()
    {
        return Ok(new TitleSummaryDefaultTemplateDto
        {
            PromptTemplate = TitleSummaryConfigService.DefaultPromptTemplate,
        });
    }

    [HttpPut]
    public async Task<ActionResult> Put([FromBody] TitleSummaryConfig request, CancellationToken cancellationToken)
    {
        if (request.ModelMode == TitleSummaryModelMode.Specified)
        {
            if (request.ModelId == null)
            {
                return BadRequest("modelId is required when modelMode is specified.");
            }

            UserModel? userModel = await userModelManager.GetUserModel(currentUser.Id, request.ModelId.Value, cancellationToken);
            if (userModel == null)
            {
                return BadRequest("Invalid model permission");
            }

            if (userModel.Model.ApiType == DBApiType.OpenAIImageGeneration)
            {
                return BadRequest("Image generation model is not allowed.");
            }
        }

        UserConfig? existing = await db.UserConfigs
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.Key == TitleSummaryConfigService.UserConfigKey, cancellationToken);

        existing ??= new UserConfig
        {
            UserId = currentUser.Id,
            Key = TitleSummaryConfigService.UserConfigKey,
            Description = "Chat title summary user override",
        };

        existing.Value = configService.SerializeConfig(request);

        if (db.Entry(existing).State == EntityState.Detached)
        {
            db.UserConfigs.Add(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> Delete(CancellationToken cancellationToken)
    {
        UserConfig? existing = await db.UserConfigs
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.Key == TitleSummaryConfigService.UserConfigKey, cancellationToken);
        if (existing == null)
        {
            return NoContent();
        }

        db.UserConfigs.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
