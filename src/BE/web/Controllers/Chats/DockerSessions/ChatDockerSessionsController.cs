using Chats.BE.Controllers.Chats.DockerSessions.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Chats.DockerInterface.Exceptions;
using Chats.DockerInterface.Models;
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
        bool hasChat = await _db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == _currentUser.Id && !x.IsArchived, cancellationToken);
        if (!hasChat) return NotFound();

        DateTime now = DateTime.UtcNow;
        ChatDockerSessionDto[] sessions = await (
            from s in _db.ChatDockerSessions.AsNoTracking()
            join t in _db.ChatTurns.AsNoTracking() on s.OwnerTurnId equals t.Id
            where t.ChatId == chatId
               && s.TerminatedAt == null
               && s.ExpiresAt > now
            orderby s.Id
            select new ChatDockerSessionDto(
                s.Id,
                s.Label,
                s.Image,
                s.ContainerId,
                s.CpuCores,
                s.MemoryBytes,
                s.MaxProcesses,
                ((NetworkMode)s.NetworkMode).ToString().ToLowerInvariant(),
                s.CreatedAt,
                s.LastActiveAt,
                s.ExpiresAt)
        ).ToArrayAsync(cancellationToken);

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
        long? leafTurnId = await _db.Chats
            .Where(x => x.Id == chatId && x.UserId == _currentUser.Id && !x.IsArchived)
            .Select(x => x.LeafTurnId)
            .FirstOrDefaultAsync(cancellationToken);
        if (leafTurnId == null && !await _db.Chats.AnyAsync(x => x.Id == chatId && x.UserId == _currentUser.Id && !x.IsArchived, cancellationToken))
        {
            return NotFound();
        }

        long ownerTurnId = leafTurnId ?? 0;
        if (ownerTurnId == 0)
        {
            ownerTurnId = await _db.ChatTurns
                .Where(t => t.ChatId == chatId)
                .OrderByDescending(t => t.Id)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (ownerTurnId == 0)
        {
            return BadRequest("Chat has no turns yet. Send a message first, then create a Docker session.");
        }

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
        await _docker.EnsureImageAsync(effectiveImage, cancellationToken);
        ContainerInfo container = await _docker.CreateContainerAsync(effectiveImage, effectiveLimits, effectiveNetworkMode, cancellationToken);

        string label = string.IsNullOrWhiteSpace(request.Label)
            ? ComputeSessionIdFromContainerId(container.ContainerId)
            : request.Label.Trim();

        DateTime nowUtc = DateTime.UtcNow;
        ChatDockerSession dbSession = new()
        {
            OwnerTurnId = ownerTurnId,
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
            dbSession.Id,
            dbSession.Label,
            dbSession.Image,
            dbSession.ContainerId,
            dbSession.CpuCores,
            dbSession.MemoryBytes,
            dbSession.MaxProcesses,
            effectiveNetworkMode.ToString().ToLowerInvariant(),
            dbSession.CreatedAt,
            dbSession.LastActiveAt,
            dbSession.ExpiresAt);
    }

    [HttpPost("{sessionId}/run-command")]
    public async Task<IActionResult> RunCommand(
        string encryptedChatId,
        [Required] string sessionId,
        [FromBody] RunCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
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
                PathSafety.WorkDir,
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

    [HttpGet("{sessionId}/files")]
    public async Task<ActionResult<DirectoryListResponse>> ListDirectory(
        string encryptedChatId,
        [Required] string sessionId,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string target = string.IsNullOrWhiteSpace(path) ? PathSafety.WorkDir : PathSafety.NormalizeUnderWorkDir(path);
        try
        {
            List<FileEntry> entries = await _docker.ListDirectoryAsync(session.ContainerId, target, cancellationToken);
            await TouchSession(session.Id, cancellationToken);
            return new DirectoryListResponse(
                target,
                entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (ContainerPathNotFoundException)
        {
            return BadRequest($"Directory not found: {target}");
        }
        catch (ContainerNotFoundException)
        {
            return BadRequest("Docker session container not found or has been deleted");
        }
    }

    [HttpPost("{sessionId}/upload")]
    [RequestSizeLimit(1024L * 1024 * 200)]
    public async Task<IActionResult> UploadFiles(
        string encryptedChatId,
        [Required] string sessionId,
        [FromQuery] string? dir,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        if (files == null || files.Count == 0) return BadRequest("No files");

        string targetDir = string.IsNullOrWhiteSpace(dir) ? PathSafety.WorkDir : PathSafety.NormalizeUnderWorkDir(dir);
        foreach (IFormFile file in files)
        {
            if (file.Length <= 0) continue;
            string safeName = PathSafety.SanitizeFileName(file.FileName);
            string targetPath = $"{targetDir.TrimEnd('/')}/{safeName}";
            await using Stream stream = file.OpenReadStream();
            using MemoryStream ms = new();
            await stream.CopyToAsync(ms, cancellationToken);
            await _docker.UploadFileAsync(session.ContainerId, targetPath, ms.ToArray(), cancellationToken);
        }

        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpGet("{sessionId}/download")]
    public async Task<IActionResult> DownloadFile(
        string encryptedChatId,
        [Required] string sessionId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string normalized = PathSafety.NormalizeUnderWorkDir(path);
        byte[] bytes = await _docker.DownloadFileAsync(session.ContainerId, normalized, cancellationToken);
        await TouchSession(session.Id, cancellationToken);

        string fileName = Path.GetFileName(normalized);
        if (!_contentTypeProvider.TryGetContentType(fileName, out string? contentType))
        {
            contentType = "application/octet-stream";
        }
        return File(bytes, contentType, fileName);
    }

    [HttpDelete("{sessionId}/file")]
    public async Task<IActionResult> DeleteFile(
        string encryptedChatId,
        [Required] string sessionId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string normalized = PathSafety.NormalizeUnderWorkDir(path);
        string cmd = _codePodConfig.GetDeleteFileCommand(normalized);
        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);
        await _docker.ExecuteCommandAsync(session.ContainerId, shellPrefix, cmd, PathSafety.WorkDir, timeoutSeconds: 60, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpPost("{sessionId}/mkdir")]
    public async Task<IActionResult> Mkdir(
        string encryptedChatId,
        [Required] string sessionId,
        [FromBody] MkdirRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string normalized = PathSafety.NormalizeUnderWorkDir(request.Path);
        string cmd = _codePodConfig.GetMkdirCommand(normalized);
        string[] shellPrefix = ParseShellPrefixCsv(session.ShellPrefix);
        await _docker.ExecuteCommandAsync(session.ContainerId, shellPrefix, cmd, PathSafety.WorkDir, timeoutSeconds: 60, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
    }

    [HttpGet("{sessionId}/text-file")]
    public async Task<ActionResult<TextFileResponse>> ReadTextFile(
        string encryptedChatId,
        [Required] string sessionId,
        [FromQuery, Required] string path,
        CancellationToken cancellationToken)
    {
        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string normalized = PathSafety.NormalizeUnderWorkDir(path);
        byte[] bytes = await _docker.DownloadFileAsync(session.ContainerId, normalized, cancellationToken);
        await TouchSession(session.Id, cancellationToken);

        const int maxTextBytes = 1 * 1024 * 1024;
        if (bytes.LongLength > maxTextBytes)
        {
            return new TextFileResponse(normalized, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }

        int sampleLen = (int)Math.Min(bytes.Length, 4096);
        ReadOnlySpan<byte> sample = bytes.AsSpan(0, sampleLen);
        if (sample.Contains((byte)0))
        {
            return new TextFileResponse(normalized, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }

        try
        {
            UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = strictUtf8.GetString(sample);
            string text = strictUtf8.GetString(bytes);
            return new TextFileResponse(normalized, IsText: true, SizeBytes: bytes.LongLength, Text: text);
        }
        catch
        {
            return new TextFileResponse(normalized, IsText: false, SizeBytes: bytes.LongLength, Text: null);
        }
    }

    [HttpPut("{sessionId}/text-file")]
    public async Task<IActionResult> SaveTextFile(
        string encryptedChatId,
        [Required] string sessionId,
        [FromBody] SaveTextFileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int chatId = _idEncryption.DecryptChatId(encryptedChatId);
        ChatDockerSession? session = await GetActiveSessionForChat(chatId, sessionId, cancellationToken);
        if (session == null) return NotFound();

        string normalized = PathSafety.NormalizeUnderWorkDir(request.Path);
        byte[] bytes = Encoding.UTF8.GetBytes(request.Text ?? string.Empty);
        const int maxTextBytes = 1 * 1024 * 1024;
        if (bytes.LongLength > maxTextBytes)
        {
            return BadRequest("Text file too large (max 1MB).");
        }

        await _docker.UploadFileAsync(session.ContainerId, normalized, bytes, cancellationToken);
        await TouchSession(session.Id, cancellationToken);
        return Ok();
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

    private async Task<ChatDockerSession?> GetActiveSessionForChat(int chatId, string sessionId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        return await (
            from s in _db.ChatDockerSessions
            join t in _db.ChatTurns on s.OwnerTurnId equals t.Id
            where t.ChatId == chatId
               && t.Chat.UserId == _currentUser.Id
               && s.Label == sessionId
               && s.TerminatedAt == null
               && s.ExpiresAt > now
            orderby s.Id descending
            select s
        ).FirstOrDefaultAsync(cancellationToken);
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
