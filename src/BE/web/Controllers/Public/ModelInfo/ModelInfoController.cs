using Chats.Web.Controllers.Public.ModelInfo.DTOs;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Chats.Web.Controllers.Public.ModelInfo;

[ResponseCache(CacheProfileName = "ModelInfo")]
public class ModelInfoController : ControllerBase
{
    [HttpGet, Route("api/model-provider")]
    public ActionResult<short[]> List()
    {
        short[] data = ModelProviderInfo.GetAllProviderIds()
            .Select(x => (short)x)
            .ToArray();
        return Ok(data);
    }

    [HttpGet, Route("api/model-provider/{modelProviderId:int}/initial-config")]
    public ActionResult<InitialModelKeyConfigDto> GetInitialConfig(short modelProviderId)
    {
        DBModelProvider provider = (DBModelProvider)modelProviderId;
        if (!ModelProviderInfo.IsValidProviderId(provider))
        {
            return NotFound();
        }

        InitialModelKeyConfigDto data = new InitialModelKeyConfigDto
        {
            InitialHost = ModelProviderInfo.GetInitialHost(provider),
            InitialSecret = ModelProviderInfo.GetInitialSecret(provider),
        };
        return Ok(data);
    }
}