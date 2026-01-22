using Chats.DB;
using Chats.BE.Controllers.Api.OpenAICompatible.Dtos;
using Chats.BE.Services;
using Chats.BE.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Api.OpenAICompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public class OpenAIListModelsController(
    CurrentApiKey currentApiKey,
    UserModelManager userModelManager) : ControllerBase
{
    [HttpGet("v1/models")]
    public async Task<ActionResult<ModelListDto>> GetModels(CancellationToken cancellationToken)
    {
        UserModel[] models = await userModelManager.GetValidModelsByApiKey(currentApiKey.ApiKey, cancellationToken);
        return Ok(new ModelListDto
        {
            Object = "list",
            Data = [.. models.Select(x => new ModelListItemDto
            {
                Id = x.Model.Name,
                Created = new DateTimeOffset(x.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds(),
                Object = "model",
                OwnedBy = x.Model.ModelKey.Name
            })]
        });
    }
}
