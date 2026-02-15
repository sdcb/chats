using Chats.BE.Controllers.Chats.DockerSessions.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Docker.DotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace Chats.BE.Controllers.Chats.DockerSessions;

[Route("api/chat/{encryptedChatId}/docker-sessions"), Authorize]
public sealed class ChatDockerSessionsController(
    ChatsDB db,
    CurrentUser currentUser,
    IUrlEncryptionService idEncryption,
    IDockerService docker,
    IOptions<CodePodConfig> codePodConfig,
    IOptions<CodeInterpreterOptions> options) : ControllerBase
{
    private readonly ChatsDB _db = db;
    private readonly CurrentUser _currentUser = currentUser;
    private readonly IUrlEncryptionService _idEncryption = idEncryption;
    private readonly IDockerService _docker = docker;
    private readonly CodePodConfig _codePodConfig = codePodConfig.Value;
    private readonly CodeInterpreterOptions _options = options.Value;

    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    private static readonly ReadOnlyMemory<byte> _dataU8 = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> _lfU8 = "\r\n\r\n"u8.ToArray();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatDockerSessionDto>>> ListActiveSessions(
        string encryptedChatId,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);

        DateTime now = DateTime.UtcNow;
        ChatDockerSessionDto[] sessions = await _db.ChatDockerSessions
            .Where(x => (x.OwnerChatId == chatId)
                     && x.TerminatedAt == null
                     && x.ExpiresAt > now)
            .Select(s => new ChatDockerSessionDto(
                _idEncryption.Encrypt(s.Id, EncryptionPurpose.DockerSessionId),
                s.Label,
                s.Image,
                s.OwnerTurnId != null,
                s.OwnerTurnId == null ? null : s.OwnerTurn!.SpanId,
                s.CpuCores,
                s.MemoryBytes,
                s.MaxProcesses,
                ((NetworkMode)s.NetworkMode).ToString().ToLowerInvariant(),
                s.Ip,
                s.CreatedAt,
                s.LastActiveAt,
                s.ExpiresAt))
            .ToArrayAsync(cancellationToken);

        return sessions;
    }

    [HttpPost]
    public async Task<ActionResult<ChatDockerSessionDto>> CreateSession(
        string encryptedChatId,
        [FromBody] CreateChatDockerSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);

        ResourceLimits max = _options.BuildMaxResourceLimits();
        ResourceLimits defaults = _options.BuildDefaultResourceLimits();
        defaults.Validate(max);

        NetworkMode effectiveNetworkMode = _options.GetDefaultNetworkMode();
        if (!string.IsNullOrWhiteSpace(request.NetworkMode))
        {
            try
            {
                effectiveNetworkMode = ParseNetworkMode(request.NetworkMode);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        NetworkMode maxAllowedNetworkMode = _options.GetMaxAllowedNetworkMode();
        if ((int)effectiveNetworkMode > (int)maxAllowedNetworkMode)
        {
            string allowed = _options.GetAllowedNetworkModesDisplay();
            return BadRequest(
                $"Requested networkMode '{effectiveNetworkMode.ToString().ToLowerInvariant()}' exceeds MaxAllowedNetworkMode " +
                $"'{maxAllowedNetworkMode.ToString().ToLowerInvariant()}'. Allowed: {allowed}.");
        }

        ResourceLimits effectiveLimits = defaults.Clone();
        if (request.MemoryBytes != null) effectiveLimits.MemoryBytes = request.MemoryBytes.Value;
        if (request.CpuCores != null) effectiveLimits.CpuCores = request.CpuCores.Value;
        if (request.MaxProcesses != null) effectiveLimits.MaxProcesses = request.MaxProcesses.Value;
        try
        {
            effectiveLimits.Validate(max);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        string effectiveImage = string.IsNullOrWhiteSpace(request.Image) ? _options.DefaultImage : request.Image.Trim();
        await foreach (CommandOutputEvent _ in _docker.EnsureImageAsync(effectiveImage, cancellationToken))
        {
            // 消费进度输出
        }
        ContainerInfo container = await _docker.CreateContainerCoreAsync(effectiveImage, effectiveLimits, effectiveNetworkMode, cancellationToken);

        string label = string.IsNullOrWhiteSpace(request.Label)
            ? ComputeSessionIdFromContainerId(container.ContainerId)
            : request.Label.Trim();

        DateTime nowUtc = DateTime.UtcNow;
        ChatDockerSession dbSession = new()
        {
            OwnerChatId = chatId,
            Label = label,
            ContainerId = container.ContainerId,
            Image = effectiveImage,
            ShellPrefix = ToShellPrefixCsv(container.ShellPrefix),
            Ip = container.Ip,
            MemoryBytes = effectiveLimits.MemoryBytes == 0 ? null : effectiveLimits.MemoryBytes,
            CpuCores = effectiveLimits.CpuCores == 0 ? null : (float)effectiveLimits.CpuCores,
            MaxProcesses = effectiveLimits.MaxProcesses == 0 ? null : (short)Math.Min(short.MaxValue, effectiveLimits.MaxProcesses),
            NetworkMode = (byte)effectiveNetworkMode,
            CreatedAt = nowUtc,
            LastActiveAt = nowUtc,
            ExpiresAt = nowUtc.AddSeconds(_options.SessionIdleTimeoutSeconds),
        };

        _db.ChatDockerSessions.Add(dbSession);
        await _db.SaveChangesAsync(cancellationToken);

        return new ChatDockerSessionDto(
            _idEncryption.Encrypt(dbSession.Id, EncryptionPurpose.DockerSessionId),
            dbSession.Label,
            dbSession.Image,
            false,
            null,
            dbSession.CpuCores,
            dbSession.MemoryBytes,
            dbSession.MaxProcesses,
            effectiveNetworkMode.ToString().ToLowerInvariant(),
            dbSession.Ip,
            dbSession.CreatedAt,
            dbSession.LastActiveAt,
            dbSession.ExpiresAt);
    }

    [HttpDelete("{encryptedSessionId}")]
    public async Task<IActionResult> DeleteSession(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        // 删除 Docker 容器
        try
        {
            await _docker.DeleteContainerAsync(session.ContainerId, cancellationToken);
        }
        catch
        {
            // 容器可能已不存在，忽略错误继续删除数据库记录
        }

        // 标记数据库记录为已终止
        session.TerminatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{encryptedSessionId}/run-command")]
    public async Task<IActionResult> RunCommand(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromBody] RunCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string command = request.Command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return BadRequest("command is required");
        }

        int timeout = _options.GetEffectiveTimeoutSeconds(request.TimeoutSeconds);
        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);

        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (CommandOutputEvent e in _docker.ExecuteCommandStreamAsync(
                session.ContainerId,
                shellPrefix,
                command,
                _codePodConfig.WorkDir,
                timeout,
                cancellationToken))
            {
                CommandStreamLine line = e switch
                {
                    CommandStdoutEvent o => new CommandStdoutLine(o.Data),
                    CommandStderrEvent o => new CommandStderrLine(o.Data),
                    CommandExitEvent o => new CommandExitLine(o.ExitCode, o.ExecutionTimeMs),
                    _ => new CommandErrorLine($"Unknown event: {e.GetType().Name}")
                };
                await Yield(line, cancellationToken);

                if (e is CommandExitEvent)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            await Yield(new CommandErrorLine(ex.Message), CancellationToken.None);
        }
        finally
        {
            await TouchSession(session.Id, cancellationToken);
        }

        return new EmptyResult();
    }

    [HttpGet("{encryptedSessionId}/files")]
    public async Task<ActionResult<DirectoryListResponse>> ListDirectory(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string target = string.IsNullOrWhiteSpace(path) ? _codePodConfig.WorkDir : path;
        try
        {
            List<FileEntry> entries = await _docker.ListDirectoryAsync(session.ContainerId, target, cancellationToken);
            await TouchSession(session.Id, cancellationToken);
            return new DirectoryListResponse(
                target,
                entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (FileNotFoundException e)
        {
            return BadRequest(e.Message);
        }
        catch (DockerContainerNotFoundException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpPost("{encryptedSessionId}/upload")]
    [RequestSizeLimit(1024L * 1024 * 200)]
    public async Task<IActionResult> UploadFiles(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromQuery] string? dir,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        if (files == null || files.Count == 0) return BadRequest("No files");

        string targetDir = string.IsNullOrWhiteSpace(dir) ? _codePodConfig.WorkDir : dir;
        foreach (IFormFile file in files)
        {
            if (file.Length <= 0) continue;
            string safeName = file.FileName;
            string targetPath = $"{targetDir.TrimEnd('/')}/{safeName}";
            await using Stream stream = file.OpenReadStream();
            using MemoryStream ms = new();
            await stream.CopyToAsync(ms, cancellationToken);
            await _docker.UploadFileAsync(session.ContainerId, targetPath, ms.ToArray(), cancellationToken);
        }

        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpGet("{encryptedSessionId}/download")]
    public async Task<IActionResult> DownloadFile(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        byte[] bytes = await _docker.DownloadFileAsync(session.ContainerId, path, cancellationToken);
        await TouchSession(session.Id, cancellationToken);

        string fileName = Path.GetFileName(path);
        if (!_contentTypeProvider.TryGetContentType(fileName, out string? contentType))
        {
            contentType = "application/octet-stream";
        }
        return File(bytes, contentType, fileName);
    }

    [HttpDelete("{encryptedSessionId}/file")]
    public async Task<IActionResult> DeleteFile(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromBody, Required] DeleteFileRequest request,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string cmd = _codePodConfig.GetDeleteFileCommand(request.Path);
        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);
        await _docker.ExecuteCommandAsync(session.ContainerId, shellPrefix, cmd, _codePodConfig.WorkDir, timeoutSeconds: 60, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpPost("{encryptedSessionId}/mkdir")]
    public async Task<IActionResult> Mkdir(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromBody] MkdirRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string cmd = _codePodConfig.GetMkdirCommand(request.Path);
        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);
        await _docker.ExecuteCommandAsync(session.ContainerId, shellPrefix, cmd, _codePodConfig.WorkDir, timeoutSeconds: 60, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpGet("{encryptedSessionId}/text-file")]
    public async Task<ActionResult<TextFileResponse>> ReadTextFile(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        byte[] bytes = await _docker.DownloadFileAsync(session.ContainerId, path, cancellationToken);
        await TouchSession(session.Id, cancellationToken);

        const int maxTextBytes = 1 * 1024 * 1024;
        if (bytes.LongLength > maxTextBytes)
        {
            return new TextFileResponse(path, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }

        int sampleLen = (int)Math.Min(bytes.Length, 4096);
        ReadOnlySpan<byte> sample = bytes.AsSpan(0, sampleLen);
        if (sample.Contains((byte)0))
        {
            return new TextFileResponse(path, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }

        try
        {
            UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = strictUtf8.GetString(sample);
            string text = strictUtf8.GetString(bytes);
            return new TextFileResponse(path, IsText: true, SizeBytes: bytes.LongLength, Text: text);
        }
        catch
        {
            return new TextFileResponse(path, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }
    }

    [HttpPut("{encryptedSessionId}/text-file")]
    public async Task<IActionResult> SaveTextFile(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromBody] SaveTextFileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        byte[] bytes = Encoding.UTF8.GetBytes(request.Text ?? string.Empty);
        const int maxTextBytes = 1 * 1024 * 1024;
        if (bytes.LongLength > maxTextBytes)
        {
            return BadRequest("Text file too large (max 1MB).");
        }

        await _docker.UploadFileAsync(session.ContainerId, request.Path, bytes, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    private const string UserEnvFilePath = "/etc/profile.d/sdcb-chats-env.sh";

    [HttpGet("{encryptedSessionId}/environment-variables")]
    public async Task<ActionResult<EnvironmentVariablesResponse>> GetEnvironmentVariables(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);

        // Get all environment variables via printenv
        CommandExitEvent allEnvResult = await _docker.ExecuteCommandAsync(
            session.ContainerId,
            shellPrefix,
            "printenv",
            _codePodConfig.WorkDir,
            timeoutSeconds: 30,
            cancellationToken);

        Dictionary<string, string> allEnvVars = ParsePrintEnvOutput(allEnvResult.Stdout);

        // Get user environment variables from the user env file
        Dictionary<string, string> userEnvVars = [];
        try
        { 
            CommandExitEvent userEnvResult = await _docker.ExecuteCommandAsync(
                session.ContainerId,
                shellPrefix,
                $"cat {UserEnvFilePath} 2>/dev/null || true",
                _codePodConfig.WorkDir,
                timeoutSeconds: 30,
                cancellationToken);

            userEnvVars = ParseUserEnvFile(userEnvResult.Stdout);
        }
        catch
        {
            // File may not exist, return empty user variables
        }

        // Build system variables (exclude user variables)
        HashSet<string> userKeys = [.. userEnvVars.Keys];
        List<EnvironmentVariable> systemVariables = allEnvVars
            .Where(kv => !userKeys.Contains(kv.Key))
            .Select(kv => new EnvironmentVariable(kv.Key, kv.Value))
            .OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<EnvironmentVariable> userVariables = userEnvVars
            .Select(kv => new EnvironmentVariable(kv.Key, kv.Value))
            .OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await TouchSession(session.Id, cancellationToken);
        return new EnvironmentVariablesResponse(systemVariables, userVariables);
    }

    [HttpPut("{encryptedSessionId}/environment-variables")]
    public async Task<IActionResult> SaveUserEnvironmentVariables(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        [FromBody] SaveUserEnvironmentVariablesRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        if (_codePodConfig.IsWindowsContainer)
        {
            return BadRequest("Saving user environment variables is not supported on Windows containers.");
        }

        // Validate variable names
        foreach (EnvironmentVariable v in request.Variables)
        {
            if (string.IsNullOrWhiteSpace(v.Key))
            {
                return BadRequest("Environment variable key cannot be empty.");
            }
            if (!IsValidEnvVarName(v.Key))
            {
                return BadRequest($"Invalid environment variable name: {v.Key}. Only alphanumeric characters and underscores are allowed, and it cannot start with a digit.");
            }
        }

        // Build the shell script content (use LF line endings for Linux)
        StringBuilder sb = new();
        sb.Append("#!/bin/sh\n");
        sb.Append("# User environment variables managed by Sdcb Chats\n");
        sb.Append("# Do not edit this file manually\n");
        sb.Append('\n');
        foreach (EnvironmentVariable v in request.Variables)
        {
            // Escape single quotes in value
            string escapedValue = v.Value.Replace("'", "'\"'\"'");
            sb.Append($"export {v.Key}='{escapedValue}'\n");
        }

        string content = sb.ToString();
        byte[] bytes = Encoding.UTF8.GetBytes(content);

        await _docker.UploadFileAsync(session.ContainerId, UserEnvFilePath, bytes, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpPost("{encryptedSessionId}/touch")]
    public async Task<IActionResult> TouchDockerSession(
        string encryptedChatId,
        [Required] string encryptedSessionId,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        long sessionId = _idEncryption.DecryptAsInt64(encryptedSessionId, EncryptionPurpose.DockerSessionId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        await TouchSession(session.Id, cancellationToken);
        return NoContent();
    }

    private static Dictionary<string, string> ParsePrintEnvOutput(string output)
    {
        Dictionary<string, string> result = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                string key = line[..eqIndex].Trim();
                string value = line[(eqIndex + 1)..].TrimEnd('\r');
                result[key] = value;
            }
        }
        return result;
    }

    private static Dictionary<string, string> ParseUserEnvFile(string content)
    {
        Dictionary<string, string> result = [];
        foreach (string line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("export ", StringComparison.Ordinal))
            {
                // export KEY='value' or export KEY="value" or export KEY=value
                string rest = trimmed["export ".Length..];
                int eqIndex = rest.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = rest[..eqIndex].Trim();
                    string rawValue = rest[(eqIndex + 1)..].Trim();
                    string value = UnquoteValue(rawValue);
                    result[key] = value;
                }
            }
        }
        return result;
    }

    private static string UnquoteValue(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
                (value.StartsWith('"') && value.EndsWith('"')))
            {
                return value[1..^1].Replace("'\"'\"'", "'");
            }
        }
        return value;
    }

    private static bool IsValidEnvVarName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (char.IsDigit(name[0])) return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private async Task Yield(CommandStreamLine line, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(_dataU8, cancellationToken);
        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(line, JSON.JsonSerializerOptions), cancellationToken);
        await Response.Body.WriteAsync(_lfU8, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task TouchSession(long dockerSessionId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        await _db.ChatDockerSessions
            .Where(x => x.Id == dockerSessionId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.LastActiveAt, now)
                .SetProperty(v => v.ExpiresAt, now.AddSeconds(_options.SessionIdleTimeoutSeconds)), cancellationToken);
    }

    private async Task<ChatDockerSession?> GetActiveSessionForChat(int chatId, long sessionId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        return await _db.ChatDockerSessions
            .Where(x => x.Id == sessionId
                     && x.OwnerChatId == chatId
                     && x.OwnerChat!.UserId == _currentUser.Id
                     && x.TerminatedAt == null
                     && x.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static NetworkMode ParseNetworkMode(string networkMode)
    {
        string v = networkMode.Trim().ToLowerInvariant();
        return v switch
        {
            "none" => NetworkMode.None,
            "bridge" => NetworkMode.Bridge,
            "host" => NetworkMode.Host,
            _ => throw new ValidationException("Invalid networkMode. Expected: none|bridge|host"),
        };
    }

    private static string ComputeSessionIdFromContainerId(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException("ContainerId is empty");
        }

        string v = containerId.Trim();
        int lastColon = v.LastIndexOf(':');
        if (lastColon >= 0 && lastColon + 1 < v.Length)
        {
            v = v[(lastColon + 1)..];
        }

        int len = Math.Min(12, v.Length);
        if (len <= 0) throw new InvalidOperationException("ContainerId is invalid");
        return v[..len];
    }

    private static string[] ParseShellPrefixCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new InvalidOperationException("ShellPrefix is empty");
        }

        string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException("ShellPrefix is empty");
        }
        return parts;
    }

    private static string ToShellPrefixCsv(string[] shellPrefix)
    {
        if (shellPrefix == null || shellPrefix.Length == 0)
        {
            throw new InvalidOperationException("ShellPrefix is required");
        }
        return string.Join(',', shellPrefix);
    }
}
