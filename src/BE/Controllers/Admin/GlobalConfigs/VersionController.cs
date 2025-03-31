using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

[AuthorizeAdmin, Route("api/version")]
public class VersionController : ControllerBase
{
    // it will be replaced by CI/CD pipeline
    const int buildVersion = 0;

    [HttpGet]
    public ActionResult<int> GetCurrentVersion()
    {
        return Ok(buildVersion);
    }

    [HttpPost("check-update")]
    public async Task<ActionResult<CheckUpdateResponse>> CheckUpdate(
        [FromServices] ILogger<VersionController> logger,
        CancellationToken cancellationToken)
    {
        string tagName = "r-" + buildVersion;
        try
        {
            tagName = await GitHubReleaseChecker.SdcbChats.GetLatestReleaseTagNameAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get latest release tag name from GitHub.");
        }

        bool hasNewVersion = GitHubReleaseChecker.IsNewVersionAvailableAsync(tagName, buildVersion);
        return Ok(new CheckUpdateResponse
        {
            CurrentVersion = buildVersion,
            HasNewVersion = hasNewVersion,
            TagName = tagName,
        });
    }
}
