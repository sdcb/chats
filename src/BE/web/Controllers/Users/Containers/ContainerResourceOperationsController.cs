using Chats.BE.Infrastructure;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Containers;
using Chats.BE.Services;
using Chats.BE.Controllers.Users.Containers.Dtos;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Containers;

[ApiController]
[Authorize]
[Route("api/containers/{encryptedId}")]
public sealed class ContainerResourceOperationsController(
    ChatsDB db,
    CurrentUser currentUser,
    IUrlEncryptionService encryption,
    ContainerBackendFactory backends,
    IOptions<CodePodConfig> codePodConfig,
    IOptions<CodeInterpreterOptions> options) : ControllerBase
{
    private const string UserEnvFilePath = "/etc/profile.d/sdcb-chats-env.sh";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private static readonly ReadOnlyMemory<byte> DataPrefix = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> EventSuffix = "\r\n\r\n"u8.ToArray();

    private readonly ChatsDB _db = db;
    private readonly CurrentUser _currentUser = currentUser;
    private readonly IUrlEncryptionService _encryption = encryption;
    private readonly ContainerBackendFactory _backends = backends;
    private readonly CodePodConfig _codePodConfig = codePodConfig.Value;
    private readonly CodeInterpreterOptions _options = options.Value;

    [HttpPost("run-command")]
    public async Task<IActionResult> RunCommand(
        string encryptedId,
        [FromBody] RunContainerCommandRequest request,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Command)) return BadRequest("command is required");

        IDockerService backend = _backends.Get(resource.RuntimeNode);
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (CommandOutputEvent output in backend.ExecuteCommandStreamAsync(
                resource.BackendResourceId,
                ParseShellPrefix(resource.ShellPrefix),
                request.Command.Trim(),
                _codePodConfig.WorkDir,
                _options.GetEffectiveTimeoutSeconds(request.TimeoutSeconds),
                cancellationToken))
            {
                ContainerCommandStreamLine line = output switch
                {
                    CommandStdoutEvent stdout => new ContainerCommandStdoutLine(stdout.Data),
                    CommandStderrEvent stderr => new ContainerCommandStderrLine(stderr.Data),
                    CommandExitEvent exit => new ContainerCommandExitLine(exit.ExitCode, exit.ExecutionTimeMs),
                    _ => new ContainerCommandErrorLine($"Unknown event: {output.GetType().Name}"),
                };
                await YieldAsync(line, cancellationToken);
                if (output is CommandExitEvent) break;
            }
        }
        catch (Exception ex)
        {
            await YieldAsync(new ContainerCommandErrorLine(ex.Message), CancellationToken.None);
        }
        finally
        {
            await TouchAsync(resource.Id, cancellationToken);
        }

        return new EmptyResult();
    }

    [HttpGet("files")]
    public async Task<ActionResult<ContainerDirectoryListResponse>> ListDirectory(
        string encryptedId,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();

        string target = string.IsNullOrWhiteSpace(path) ? _codePodConfig.WorkDir : path;
        try
        {
            List<FileEntry> entries = await _backends.Get(resource.RuntimeNode)
                .ListDirectoryAsync(resource.BackendResourceId, target, cancellationToken);
            await TouchAsync(resource.Id, cancellationToken);
            return new ContainerDirectoryListResponse(
                target,
                [.. entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)]);
        }
        catch (FileNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(1024L * 1024 * 200)]
    public async Task<IActionResult> Upload(
        string encryptedId,
        [FromQuery] string? dir,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        if (files.Count == 0) return BadRequest("No files");

        IDockerService backend = _backends.Get(resource.RuntimeNode);
        string targetDirectory = string.IsNullOrWhiteSpace(dir) ? _codePodConfig.WorkDir : dir;
        foreach (IFormFile file in files.Where(x => x.Length > 0))
        {
            string targetPath = $"{targetDirectory.TrimEnd('/')}/{Path.GetFileName(file.FileName)}";
            await using Stream stream = file.OpenReadStream();
            using MemoryStream memory = new();
            await stream.CopyToAsync(memory, cancellationToken);
            await backend.UploadFileAsync(resource.BackendResourceId, targetPath, memory.ToArray(), cancellationToken);
        }

        await TouchAsync(resource.Id, cancellationToken);
        return NoContent();
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        string encryptedId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        byte[] bytes = await _backends.Get(resource.RuntimeNode)
            .DownloadFileAsync(resource.BackendResourceId, path, cancellationToken);
        await TouchAsync(resource.Id, cancellationToken);

        string fileName = Path.GetFileName(path);
        if (!ContentTypes.TryGetContentType(fileName, out string? contentType)) contentType = "application/octet-stream";
        return File(bytes, contentType, fileName);
    }

    [HttpDelete("file")]
    public async Task<IActionResult> DeleteFile(
        string encryptedId,
        [FromBody] ContainerPathRequest request,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        await ExecuteAsync(resource, _codePodConfig.GetDeleteFileCommand(request.Path), cancellationToken);
        return NoContent();
    }

    [HttpPost("mkdir")]
    public async Task<IActionResult> MakeDirectory(
        string encryptedId,
        [FromBody] ContainerPathRequest request,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        await ExecuteAsync(resource, _codePodConfig.GetMkdirCommand(request.Path), cancellationToken);
        return NoContent();
    }

    [HttpGet("text-file")]
    public async Task<ActionResult<ContainerTextFileResponse>> ReadTextFile(
        string encryptedId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        byte[] bytes = await _backends.Get(resource.RuntimeNode)
            .DownloadFileAsync(resource.BackendResourceId, path, cancellationToken);
        await TouchAsync(resource.Id, cancellationToken);

        const int maxTextBytes = 1024 * 1024;
        if (bytes.LongLength > maxTextBytes || bytes.AsSpan(0, Math.Min(bytes.Length, 4096)).Contains((byte)0))
        {
            return new ContainerTextFileResponse(path, false, bytes.LongLength, null);
        }

        try
        {
            UTF8Encoding utf8 = new(false, true);
            return new ContainerTextFileResponse(path, true, bytes.LongLength, utf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return new ContainerTextFileResponse(path, false, bytes.LongLength, null);
        }
    }

    [HttpPut("text-file")]
    public async Task<IActionResult> SaveTextFile(
        string encryptedId,
        [FromBody] SaveContainerTextFileRequest request,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        byte[] bytes = Encoding.UTF8.GetBytes(request.Text ?? string.Empty);
        if (bytes.Length > 1024 * 1024) return BadRequest("Text file too large (max 1MB).");
        await _backends.Get(resource.RuntimeNode)
            .UploadFileAsync(resource.BackendResourceId, request.Path, bytes, cancellationToken);
        await TouchAsync(resource.Id, cancellationToken);
        return NoContent();
    }

    [HttpGet("environment-variables")]
    public async Task<ActionResult<ContainerEnvironmentVariablesResponse>> EnvironmentVariables(
        string encryptedId,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        IDockerService backend = _backends.Get(resource.RuntimeNode);
        string[] shell = ParseShellPrefix(resource.ShellPrefix);
        CommandExitEvent all = await backend.ExecuteCommandAsync(
            resource.BackendResourceId,
            shell,
            "printenv",
            _codePodConfig.WorkDir,
            30,
            cancellationToken);
        CommandExitEvent managed = await backend.ExecuteCommandAsync(
            resource.BackendResourceId,
            shell,
            $"cat {UserEnvFilePath} 2>/dev/null || true",
            _codePodConfig.WorkDir,
            30,
            cancellationToken);

        Dictionary<string, string> userVariables = ParseManagedVariables(managed.Stdout);
        HashSet<string> userKeys = [.. userVariables.Keys];
        ContainerEnvironmentVariable[] system = [.. ParseEnvironment(all.Stdout)
            .Where(x => !userKeys.Contains(x.Key))
            .Select(x => new ContainerEnvironmentVariable(x.Key, x.Value))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
        ContainerEnvironmentVariable[] user = [.. userVariables
            .Select(x => new ContainerEnvironmentVariable(x.Key, x.Value))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
        await TouchAsync(resource.Id, cancellationToken);
        return new ContainerEnvironmentVariablesResponse(system, user);
    }

    [HttpPut("environment-variables")]
    public async Task<IActionResult> SaveEnvironmentVariables(
        string encryptedId,
        [FromBody] SaveContainerEnvironmentVariablesRequest request,
        CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        if (request.Variables.Any(x => !IsValidVariableName(x.Key))) return BadRequest("Invalid environment variable name.");

        StringBuilder script = new("#!/bin/sh\n# User environment variables managed by Chats\n\n");
        foreach (ContainerEnvironmentVariable variable in request.Variables)
        {
            string escaped = variable.Value.Replace("'", "'\"'\"'");
            script.Append("export ").Append(variable.Key).Append("='").Append(escaped).Append("'\n");
        }
        await _backends.Get(resource.RuntimeNode).UploadFileAsync(
            resource.BackendResourceId,
            UserEnvFilePath,
            Encoding.UTF8.GetBytes(script.ToString()),
            cancellationToken);
        await TouchAsync(resource.Id, cancellationToken);
        return NoContent();
    }

    [HttpPost("touch")]
    public async Task<IActionResult> Touch(string encryptedId, CancellationToken cancellationToken)
    {
        ContainerResource? resource = await GetRunningResourceAsync(encryptedId, cancellationToken);
        if (resource is null) return NotFound();
        await TouchAsync(resource.Id, cancellationToken);
        return NoContent();
    }

    private async Task<ContainerResource?> GetRunningResourceAsync(string encryptedId, CancellationToken cancellationToken)
    {
        long id = _encryption.DecryptAsInt64(encryptedId, EncryptionPurpose.DockerSessionId);
        DateTime now = DateTime.UtcNow;
        return await _db.ContainerResources
            .Include(x => x.RuntimeNode)
            .SingleOrDefaultAsync(x => x.Id == id
                && x.OwnerUserId == _currentUser.Id
                && x.DeletedAt == null
                && x.StoppedAt == null
                && (x.IsPermanent || x.CleanupAt == null || x.CleanupAt > now),
                cancellationToken);
    }

    private async Task ExecuteAsync(ContainerResource resource, string command, CancellationToken cancellationToken)
    {
        await _backends.Get(resource.RuntimeNode).ExecuteCommandAsync(
            resource.BackendResourceId,
            ParseShellPrefix(resource.ShellPrefix),
            command,
            _codePodConfig.WorkDir,
            60,
            cancellationToken);
        await TouchAsync(resource.Id, cancellationToken);
    }

    private async Task TouchAsync(long resourceId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        await _db.ContainerResources
            .Where(x => x.Id == resourceId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastActiveAt, now)
                .SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.CleanupAt, x => x.IsPermanent ? x.CleanupAt : now.AddSeconds(_options.SessionIdleTimeoutSeconds)),
                cancellationToken);
    }

    private async Task YieldAsync(ContainerCommandStreamLine line, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(DataPrefix, cancellationToken);
        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(line, JSON.JsonSerializerOptions), cancellationToken);
        await Response.Body.WriteAsync(EventSuffix, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static string[] ParseShellPrefix(string? shellPrefix)
        => string.IsNullOrWhiteSpace(shellPrefix)
            ? ["/bin/sh", "-lc"]
            : [.. shellPrefix.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static Dictionary<string, string> ParseEnvironment(string output)
    {
        Dictionary<string, string> result = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf('=');
            if (separator > 0) result[line[..separator].Trim()] = line[(separator + 1)..].TrimEnd('\r');
        }
        return result;
    }

    private static Dictionary<string, string> ParseManagedVariables(string output)
    {
        Dictionary<string, string> result = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string value = line.Trim();
            if (!value.StartsWith("export ", StringComparison.Ordinal)) continue;
            string assignment = value[7..];
            int separator = assignment.IndexOf('=');
            if (separator <= 0) continue;
            string raw = assignment[(separator + 1)..].Trim();
            if (raw.Length >= 2 && raw.StartsWith('\'') && raw.EndsWith('\'')) raw = raw[1..^1];
            result[assignment[..separator].Trim()] = raw.Replace("'\"'\"'", "'");
        }
        return result;
    }

    private static bool IsValidVariableName(string name)
        => !string.IsNullOrWhiteSpace(name)
            && !char.IsDigit(name[0])
            && name.All(character => char.IsLetterOrDigit(character) || character == '_');
}
