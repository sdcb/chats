using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

[AuthorizeAdmin, Route("api/version")]
public class VersionController(ILogger<VersionController> logger) : ControllerBase
{
    static string? CurrentVersion => typeof(VersionController).Assembly
        .GetCustomAttribute<AssemblyFileVersionAttribute>()?
        .Version;

    [HttpGet]
    public ActionResult<string> GetCurrentVersion()
    {
        return Ok(CurrentVersion);
    }

    [HttpPost("check-update")]
    public async Task<ActionResult<CheckUpdateResponse>> CheckUpdate(
        [FromServices] ILogger<VersionController> logger,
        CancellationToken cancellationToken)
    {
        string? tagName = null;
        try
        {
            tagName = await GitHubReleaseChecker.SdcbChats.GetLatestReleaseTagNameAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get latest release tag name from GitHub.");
        }

        bool hasNewVersion = IsNewVersionAvailableAsync(tagName, CurrentVersion);
        return Ok(new CheckUpdateResponse
        {
            CurrentVersion = CurrentVersion,
            LatestVersion = tagName,
            HasNewVersion = hasNewVersion,
        });
    }

    bool IsNewVersionAvailableAsync(string? latestTagName, string? currentVersion)
    {
        // latestTagName format: 1.0.0.587
        // currentVersion format: 1.0.0.586
        if (string.IsNullOrEmpty(latestTagName) || string.IsNullOrEmpty(currentVersion))
        {
            return false;
        }

        if (latestTagName.StartsWith("r-"))
        {
            return false;
        }

        try
        {
            Version latestVersion = Version.Parse(latestTagName);
            Version currentVersionParsed = Version.Parse(currentVersion);
            return latestVersion > currentVersionParsed;
        }
        catch (Exception)
        {
            logger.LogWarning("Failed to parse version strings: {LatestTagName}, {CurrentVersion}", latestTagName, currentVersion);
            return false;
        }
    }
}
