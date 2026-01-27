using Chats.BE.Controllers.Chats.DockerSessions.Dtos;
using Chats.BE.Services.CodeInterpreter;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Chats.BE.Controllers.Chats.DockerSessions;

[Route("api/docker-sessions"), Authorize]
public sealed class DockerSessionConfigController(
    IDockerService docker,
    IOptions<CodeInterpreterOptions> options) : ControllerBase
{
    private readonly IDockerService _docker = docker;
    private readonly CodeInterpreterOptions _options = options.Value;

    [HttpGet("default-image")]
    public ActionResult<DefaultImageResponse> GetDefaultImage()
    {
        return new DefaultImageResponse(_options.DefaultImage, _options.DefaultImageDescription);
    }

    [HttpGet("cpu-limits")]
    public ActionResult<ResourceLimitResponse> GetCpuLimits()
    {
        ResourceLimits defaults = _options.BuildDefaultResourceLimits();
        ResourceLimits max = _options.BuildMaxResourceLimits();
        return new ResourceLimitResponse(defaults.CpuCores, max.CpuCores);
    }

    [HttpGet("memory-limits")]
    public ActionResult<MemoryLimitResponse> GetMemoryLimits()
    {
        ResourceLimits defaults = _options.BuildDefaultResourceLimits();
        ResourceLimits max = _options.BuildMaxResourceLimits();
        return new MemoryLimitResponse(defaults.MemoryBytes, max.MemoryBytes);
    }

    [HttpGet("network-modes")]
    public ActionResult<NetworkModesResponse> GetNetworkModes()
    {
        NetworkMode def = _options.GetDefaultNetworkMode();
        NetworkMode maxAllowed = _options.GetMaxAllowedNetworkMode();
        IReadOnlyList<string> allowed = Enum.GetValues<NetworkMode>()
            .Where(m => (int)m <= (int)maxAllowed)
            .Select(m => m.ToString().ToLowerInvariant())
            .ToArray();
        return new NetworkModesResponse(def.ToString().ToLowerInvariant(), maxAllowed.ToString().ToLowerInvariant(), allowed);
    }

    [HttpGet("images")]
    public async Task<ActionResult<ImageListResponse>> ListImages(CancellationToken cancellationToken)
    {
        List<string> images = await _docker.ListImagesAsync(cancellationToken);
        return new ImageListResponse(images);
    }
}

