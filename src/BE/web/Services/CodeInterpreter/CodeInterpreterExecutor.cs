using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Infrastructure.Functional;
using Chats.DB;
using DBFile = Chats.DB.File;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterExecutor(
    IDockerService docker,
    FileServiceFactory fileServiceFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<CodePodConfig> codePodConfig,
    IOptions<CodeInterpreterOptions> options,
    ILogger<CodeInterpreterExecutor> logger)
{
    private static readonly AttributedToolRegistry _toolRegistry = new(typeof(CodeInterpreterExecutor));

    private readonly IDockerService _docker = docker;
    private readonly FileServiceFactory _fileServiceFactory = fileServiceFactory;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly CodePodConfig _codePodConfig = codePodConfig.Value;
    private readonly CodeInterpreterOptions _options = options.Value;
    private readonly ILogger<CodeInterpreterExecutor> _logger = logger;

    public sealed record PendingFileArtifact(string SourcePath, string FileName, string ContentType, byte[] Bytes);

    public static readonly string[] ToolNames = _toolRegistry.ToolNames.ToArray();

    public void AddTools(ICollection<ChatTool> tools)
    {
        Dictionary<string, string> placeholders = BuildPlaceholderReplacements();
        foreach (AttributedToolRegistry.ToolDescriptor d in _toolRegistry.Tools)
        {
            string schemaJson = ReplacePlaceholders(d.SchemaJson, placeholders);
            tools.Add(FunctionTool.Create(d.ToolName, d.Description, schemaJson));
        }
    }

    private Dictionary<string, string> BuildPlaceholderReplacements()
    {
        ResourceLimits defaultLimits = _options.BuildDefaultResourceLimits();
        string resourceLimitsStr = FormatResourceLimits(defaultLimits);
        string networkModeStr = _options.GetDefaultNetworkMode().ToString().ToLowerInvariant();
        string timeoutStr = _options.DefaultTimeoutSeconds?.ToString() ?? "unlimited";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = timeoutStr,
            ["{defaultResourceLimits}"] = resourceLimitsStr,
            ["{defaultNetworkMode}"] = networkModeStr,
            ["{defaultImage}"] = _options.DefaultImage,
        };
    }

    internal static string FormatResourceLimits(ResourceLimits limits)
    {
        List<string> parts = [];
        if (limits.MemoryBytes > 0)
        {
            parts.Add($"memory={FormatBytes(limits.MemoryBytes)}");
        }
        else
        {
            parts.Add("memory=unlimited");
        }

        if (limits.CpuCores > 0)
        {
            parts.Add($"cpu={limits.CpuCores:0.##} cores");
        }
        else
        {
            parts.Add("cpu=unlimited");
        }

        if (limits.MaxProcesses > 0)
        {
            parts.Add($"maxProcesses={limits.MaxProcesses}");
        }
        else
        {
            parts.Add("maxProcesses=unlimited");
        }

        return string.Join(", ", parts);
    }

    internal static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):0.##}GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):0.##}MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:0.##}KB";
        return $"{bytes}B";
    }

    internal static string ReplacePlaceholders(string input, Dictionary<string, string> placeholders)
    {
        foreach (KeyValuePair<string, string> kvp in placeholders)
        {
            input = input.Replace(kvp.Key, kvp.Value);
        }
        return input;
    }

    public bool IsCodeInterpreterTool(string toolName)
        => _toolRegistry.Contains(toolName);

    public NeutralSystemMessage BuildSystemMessage(string? existingSystemPrompt, IEnumerable<Step> messageSteps)
    {
        StringBuilder sb = new();
        if (!string.IsNullOrWhiteSpace(existingSystemPrompt))
        {
            sb.AppendLine(existingSystemPrompt);
            sb.AppendLine();
        }

        sb.AppendLine("You have access to a sandboxed code interpreter environment.");
        sb.AppendLine($"- Working directory: {PathSafety.WorkDir}");
        sb.AppendLine($"- Artifacts directory: {PathSafety.WorkDir}/artifacts");
        sb.AppendLine("- No network by default; use networkMode only if required.");
        sb.AppendLine("- To use files from the chat, you MUST call download_files first.");

        return NeutralSystemMessage.FromText(sb.ToString());
    }

    public string? BuildCloudFilesContextPrefix(IEnumerable<Step> messageSteps)
    {
        List<(int fileId, string fileName, DBFile file)> cloudFiles = CollectCloudFiles(messageSteps);

        if (cloudFiles.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new();
        sb.AppendLine("[Environment Info]");
        sb.AppendLine("Cloud Files Available:");
        foreach ((int fileId, string fileName, DBFile file) in cloudFiles.OrderBy(x => x.fileId))
        {
            sb.AppendLine($"- {fileName} (id={fileId}, size={file.Size}, type={file.MediaType})");
        }
        sb.AppendLine("Use download_files with wildcard patterns matching the file names above.");
        return sb.ToString();
    }

    public sealed class TurnContext
    {
        public required IReadOnlyList<ChatTurn> MessageTurns { get; init; }
        public required IReadOnlyList<Step> MessageSteps { get; init; }
        public required ChatTurn CurrentAssistantTurn { get; init; }
        public required int ClientInfoId { get; init; }

        public Dictionary<string, SessionState> SessionsBySessionId { get; } = new(StringComparer.Ordinal);

        public sealed class SessionState
        {
            public required ChatDockerSession DbSession { get; init; }
            public Dictionary<string, FileEntry> ArtifactsSnapshot { get; set; } = new(StringComparer.Ordinal);
            public bool SnapshotTaken { get; set; }
            public bool UsedInThisTurn { get; set; }
            public int? DefaultTimeoutSeconds { get; set; }

            public List<PendingFileArtifact> PendingArtifacts { get; } = [];
            public HashSet<string> PendingArtifactPaths { get; } = new(StringComparer.Ordinal);
            public long PendingArtifactsBytesThisTurn { get; set; }
        }
    }

    public async Task<Result<string>> ExecuteToolCallAsync(TurnContext ctx, string toolName, string rawJsonArgs, CancellationToken cancellationToken)
    {
        try
        {
            return await _toolRegistry.InvokeAsync(this, ctx, toolName, rawJsonArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CodeInterpreter tool failed: {toolName}", toolName);
            return Result.Fail<string>(ex.Message);
        }
    }

    public async Task FinalizeTurnAndAttachFilesAsync(TurnContext ctx, Step assistantStep, CancellationToken cancellationToken)
    {
        // NOTE: Artifacts are now synced after each tool call (run_command/write_file).
        // End-of-turn should not upload artifacts.
        foreach (TurnContext.SessionState state in ctx.SessionsBySessionId.Values.Where(s => s.UsedInThisTurn))
        {
            await TouchSession(state.DbSession, cancellationToken);
        }
    }

    public List<PendingFileArtifact> DrainPendingArtifacts(TurnContext ctx)
    {
        List<PendingFileArtifact> result = [];
        foreach (TurnContext.SessionState state in ctx.SessionsBySessionId.Values)
        {
            if (state.PendingArtifacts.Count == 0) continue;
            result.AddRange(state.PendingArtifacts);
            state.PendingArtifacts.Clear();
            state.PendingArtifactPaths.Clear();
        }
        return result;
    }

    internal sealed record ResourceLimitsArgs(long? MemoryBytes = null, double? CpuCores = null, long? MaxProcesses = null);

    [ToolFunction("Create a docker session")]
    private async Task<Result<string>> CreateDockerSession(
        TurnContext ctx,
        [ToolParam("Docker image to use (null means use server default: {defaultImage}).")]
        string? image,
        [ToolParam("Label. If empty, server will use the new container id prefix (first 12 chars).")]
        string? label,
        [ToolParam("Default command timeout seconds (null means use server default: {defaultTimeoutSeconds}).")]
        int? timeoutSeconds,
        [ToolParam("Resource limits (null means use server default: {defaultResourceLimits}).")]
        ResourceLimitsArgs? resourceLimits,
        [ToolParam("Network mode. One of: none, bridge, host. null means use server default: {defaultNetworkMode}.")]
        [EnumDataType(typeof(NetworkMode))]
        string? networkMode,
        CancellationToken cancellationToken)
    {
        bool hasLabel = !string.IsNullOrWhiteSpace(label);

        // Reuse from in-memory first (only possible when label provided).
        if (hasLabel && ctx.SessionsBySessionId.TryGetValue(label!, out TurnContext.SessionState? existing))
        {
            existing.UsedInThisTurn = true;
            existing.DefaultTimeoutSeconds ??= timeoutSeconds;
            return Result.Ok($"sessionId: {label}\ncontainerId: {existing.DbSession.ContainerId}\nworkdir: {PathSafety.WorkDir}");
        }

        // Resolve from DB along the ParentId chain of the current generating turn.
        DateTime nowUtc = DateTime.UtcNow;
        ChatDockerSession? dbSession = null;
        if (hasLabel)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

            List<long> turnPathIds = [];
            if (ctx.MessageTurns.Count > 0)
            {
                // In normal request flow, MessageTurns is the authoritative in-memory parent chain.
                // If it's not complete, that's a bug and we should fail fast rather than silently hitting DB.
                turnPathIds = LoadTurnPathIdsFromMessageTurns(ctx);
            }
            if (turnPathIds.Count > 0)
            {
                List<ChatDockerSession> candidates = await db.ChatDockerSessions
                    .AsNoTracking()
                    .Where(x => x.TerminatedAt == null && x.ExpiresAt > nowUtc && x.Label == label && turnPathIds.Contains(x.OwnerTurnId))
                    .OrderByDescending(x => x.Id)
                    .ToListAsync(cancellationToken);

                if (candidates.Count > 0)
                {
                    Dictionary<long, int> depth = turnPathIds
                        .Select((id, idx) => (id, idx))
                        .ToDictionary(x => x.id, x => x.idx);

                    dbSession = candidates
                        .OrderBy(x => depth.GetValueOrDefault(x.OwnerTurnId, int.MaxValue))
                        .ThenByDescending(x => x.Id)
                        .FirstOrDefault();
                }
            }
        }

        int? defaultTimeoutSeconds = timeoutSeconds;

        if (dbSession == null)
        {
            // Build effective limits/network mode.
            ResourceLimits limits = _options.BuildDefaultResourceLimits();
            ResourceLimits max = _options.BuildMaxResourceLimits();
            limits.Validate(max);

            NetworkMode effectiveNetworkMode = _options.GetDefaultNetworkMode();
            if (!string.IsNullOrWhiteSpace(networkMode))
            {
                effectiveNetworkMode = ParseNetworkMode(networkMode);
            }

            if (resourceLimits != null)
            {
                limits = MergeLimitsWithDefaults(resourceLimits);
                limits.Validate(max);
            }

            string effectiveImage = string.IsNullOrWhiteSpace(image) ? _options.DefaultImage : image;
            await _docker.EnsureImageAsync(effectiveImage, cancellationToken);
            ContainerInfo container = await _docker.CreateContainerAsync(effectiveImage, limits, effectiveNetworkMode, cancellationToken);

            if (!hasLabel)
            {
                label = ComputeSessionIdFromContainerId(container.ContainerId);

                // Ultra defensive: avoid collisions (extremely unlikely).
                if (ctx.SessionsBySessionId.ContainsKey(label))
                {
                    label = $"{label}{GenerateAlphaFirstToken(4)}";
                }
            }

            DateTime now = nowUtc;
            dbSession = new ChatDockerSession
            {
                OwnerTurnId = ctx.CurrentAssistantTurn.Id,
                Label = label!,
                ContainerId = container.ContainerId,
                Image = effectiveImage,
                MemoryBytes = limits.MemoryBytes == 0 ? null : limits.MemoryBytes,
                CpuCores = limits.CpuCores == 0 ? null : (float)limits.CpuCores,
                MaxProcesses = limits.MaxProcesses == 0 ? null : (short)Math.Min(short.MaxValue, limits.MaxProcesses),
                NetworkMode = (byte)effectiveNetworkMode,
                CreatedAt = now,
                LastActiveAt = now,
                ExpiresAt = now.AddSeconds(_options.SessionIdleTimeoutSeconds),
            };

            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
                db.ChatDockerSessions.Add(dbSession);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        TurnContext.SessionState state = new()
        {
            DbSession = dbSession,
            DefaultTimeoutSeconds = defaultTimeoutSeconds,
            UsedInThisTurn = true,
        };
        ctx.SessionsBySessionId[label!] = state;

        if (!state.SnapshotTaken)
        {
            state.ArtifactsSnapshot = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);
            state.SnapshotTaken = true;
        }

        return Result.Ok($"sessionId: {label}\nimage: {dbSession.Image}");
    }

    private static long? GetSessionLookupStartTurnId(TurnContext ctx)
    {
        if (ctx.CurrentAssistantTurn.Id > 0)
        {
            return ctx.CurrentAssistantTurn.Id;
        }

        // In controller request flow, the current assistant turn is new (Id=0).
        // MessageTurns is a parent-chain ending at req.LastMessageId, which is the correct anchor for session reuse.
        if (ctx.MessageTurns.Count > 0)
        {
            long lastId = ctx.MessageTurns[^1].Id;
            if (lastId > 0) return lastId;
        }

        if (ctx.CurrentAssistantTurn.ParentId != null)
        {
            return ctx.CurrentAssistantTurn.ParentId.Value;
        }

        return ctx.CurrentAssistantTurn.Parent?.Id;
    }

    private static List<long> LoadTurnPathIdsFromMessageTurns(TurnContext ctx)
    {
        long? start = GetSessionLookupStartTurnId(ctx);
        if (start == null)
        {
            throw new InvalidOperationException("Cannot determine start turn id for session lookup. Ensure MessageTurns includes the last message turn (and/or CurrentAssistantTurn has an id).");
        }

        ChatTurn[] candidatesTurns = [.. ctx.MessageTurns, ctx.CurrentAssistantTurn];
        Dictionary<long, long?> parentById = candidatesTurns
            .Where(t => t.Id > 0)
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.Last().ParentId);

        List<long> ids = [];
        long? current = start;
        while (current != null)
        {
            long id = current.Value;
            ids.Add(id);

            if (ids.Count > 512)
            {
                break;
            }

            if (!parentById.TryGetValue(id, out long? parent))
            {
                throw new InvalidOperationException($"MessageTurns is incomplete: missing turnId={id} in parent chain.");
            }

            current = parent;
        }

        return ids;
    }

    [ToolFunction("Destroy the docker session")]
    private async Task<Result<string>> DestroySession(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);

        try
        {
            await _docker.DeleteContainerAsync(state.DbSession.ContainerId, cancellationToken);
        }
        catch
        {
            // best-effort
        }

        state.DbSession.TerminatedAt = DateTime.UtcNow;
        await TouchSession(state.DbSession, cancellationToken);

        ctx.SessionsBySessionId.Remove(sessionId);

        return Result.Ok($"Destroyed session: {sessionId}");
    }

    [ToolFunction("Run a shell command inside the session workdir /app")]
    private async Task<Result<string>> RunCommand(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        [ToolParam("Shell command to run")]
        [Required]
        string command,
        [ToolParam("Command timeout seconds (null means use server default).")]
        int? timeoutSeconds,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);
        state.UsedInThisTurn = true;

        int? requestedTimeout = timeoutSeconds ?? state.DefaultTimeoutSeconds;
        int timeout = _options.GetEffectiveTimeoutSeconds(requestedTimeout);

        CommandResult result = await _docker.ExecuteCommandAsync(state.DbSession.ContainerId, command, PathSafety.WorkDir, timeout, cancellationToken);
        await TouchSession(state.DbSession, cancellationToken);

        await SyncArtifactsAfterToolCall(ctx, state, cancellationToken);

        return Result.Ok(FormatRunCommandResult(result));
    }

    private static string FormatRunCommandResult(CommandResult result)
    {
        string stdout = result.Stdout ?? string.Empty;
        string stderr = result.Stderr ?? string.Empty;

        bool isCleanSuccess = result.ExitCode == 0 && !result.IsTruncated && string.IsNullOrWhiteSpace(stderr);
        if (isCleanSuccess)
        {
            return string.IsNullOrWhiteSpace(stdout) ? "(no output)" : stdout;
        }

        StringBuilder sb = new();
        sb.AppendLine($"ExitCode: {result.ExitCode}");
        sb.AppendLine($"ExecutionTimeMs: {result.ExecutionTimeMs}");
        if (result.IsTruncated) sb.AppendLine("IsTruncated: true");

        if (!string.IsNullOrEmpty(stdout))
        {
            sb.AppendLine("Stdout:");
            sb.AppendLine(stdout);
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            sb.AppendLine("Stderr:");
            sb.AppendLine(stderr);
        }

        return sb.ToString().TrimEnd();
    }

    [ToolFunction("Write a file under /app")]
    private async Task<Result<string>> WriteFile(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        [ToolParam("Path under /app.")]
        [Required]
        string path,
        [ToolParam("UTF-8 text content.")]
        string? text,
        [ToolParam("Base64-encoded bytes.")]
        string? contentBase64,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);
        state.UsedInThisTurn = true;

        string normalizedPath = PathSafety.NormalizeUnderWorkDir(path);

        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(contentBase64))
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        else if (!string.IsNullOrEmpty(text))
        {
            bytes = Encoding.UTF8.GetBytes(text);
        }
        else
        {
            return Result.Fail<string>("Either text or contentBase64 is required");
        }

        await _docker.UploadFileAsync(state.DbSession.ContainerId, normalizedPath, bytes, cancellationToken);
        await TouchSession(state.DbSession, cancellationToken);

        await SyncArtifactsAfterToolCall(ctx, state, cancellationToken);

        return Result.Ok($"Wrote {bytes.Length} bytes to {normalizedPath}");
    }

    [ToolFunction("Read a file under /app")]
    private async Task<Result<string>> ReadFile(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        [ToolParam("Absolute path to the file (must be under /app).")]
        [Required]
        string path,
        [ToolParam("Optional start line (1-based, inclusive).")]
        int? startLine,
        [ToolParam("Optional end line (1-based, inclusive).")]
        int? endLine,
        [ToolParam("Optional (default false). If true, prefix each line with its line number; the first line will be the total line count.")]
        bool? withLineNumbers,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);
        state.UsedInThisTurn = true;

        string normalizedPath = PathSafety.NormalizeUnderWorkDir(path);

        byte[] bytes = await _docker.DownloadFileAsync(state.DbSession.ContainerId, normalizedPath, cancellationToken);

        string output;
        bool wantsLineNumbers = withLineNumbers == true;

        // Try decode as UTF-8 text first; fallback to base64 summary for binary.
        string? fullText = null;
        try
        {
            fullText = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch
        {
            // ignore
        }

        if (fullText != null)
        {
            string[] lines = fullText.Split(["\r\n", "\n"], StringSplitOptions.None);
            int totalLines = lines.Length;

            if (startLine is < 1)
            {
                return Result.Fail<string>("startLine must be >= 1");
            }
            if (endLine is < 1)
            {
                return Result.Fail<string>("endLine must be >= 1");
            }
            if (startLine != null && endLine != null && endLine.Value < startLine.Value)
            {
                return Result.Fail<string>("endLine must be >= startLine");
            }

            if (totalLines == 0)
            {
                output = wantsLineNumbers ? $"TotalLines: {totalLines}" : string.Empty;
            }
            else
            {
                int effectiveStart = startLine ?? 1;
                int effectiveEnd = endLine ?? totalLines;

                // If start is beyond EOF, return empty content.
                if (effectiveStart > totalLines)
                {
                    output = wantsLineNumbers ? $"TotalLines: {totalLines}" : string.Empty;
                }
                else
                {
                    effectiveStart = Math.Clamp(effectiveStart, 1, totalLines);
                    effectiveEnd = Math.Clamp(effectiveEnd, 1, totalLines);

                    // If range collapses, return empty content.
                    if (effectiveStart > effectiveEnd)
                    {
                        output = wantsLineNumbers ? $"TotalLines: {totalLines}" : string.Empty;
                    }
                    else
                    {
                StringBuilder sb = new();
                if (wantsLineNumbers)
                {
                    sb.Append("TotalLines: ").Append(totalLines).Append('\n');
                }

                for (int i = effectiveStart; i <= effectiveEnd; i++)
                {
                    string line = lines[i - 1];
                    if (wantsLineNumbers)
                    {
                        sb.Append(i).Append(": ");
                    }
                    sb.Append(line);

                    if (i != effectiveEnd)
                    {
                        sb.Append('\n');
                    }
                }

                output = sb.ToString();
                    }
                }
            }
        }
        else
        {
            // Binary: return a base64 preview; line ranges are not applicable.
            OutputOptions binaryOptions = _codePodConfig.OutputOptions;
            string prefix = wantsLineNumbers ? "TotalLines: 0\n" : string.Empty;

            // Try to keep the preview *useful* under MaxOutputBytes by accounting for base64 expansion.
            // We still apply TruncateText later as a final safety net.
            string headerTemplate = $"{prefix}Path: {normalizedPath}\nSize: {bytes.Length}\nBase64(first {{0}} bytes):\n";

            int budget = Math.Max(1, binaryOptions.MaxOutputBytes);
            int minHeaderBytes = Encoding.UTF8.GetByteCount(string.Format(headerTemplate, 0));
            int availableForBase64Chars = Math.Max(0, budget - minHeaderBytes);

            // base64 chars are ASCII; chars == bytes in UTF-8 for this portion.
            int maxRawBytesFromBase64 = (availableForBase64Chars / 4) * 3;
            int previewBytes = Math.Clamp(maxRawBytesFromBase64, 0, bytes.Length);

            // Ensure we return at least some data when possible.
            if (previewBytes == 0 && bytes.Length > 0 && availableForBase64Chars > 0)
            {
                previewBytes = 1;
            }

            byte[] slice = previewBytes == bytes.Length ? bytes : bytes[..previewBytes];
            string base64 = slice.Length == 0 ? string.Empty : Convert.ToBase64String(slice);

            string header = string.Format(headerTemplate, slice.Length);
            output = header + base64;
        }

        await TouchSession(state.DbSession, cancellationToken);

        // Apply truncation based on CodePod OutputOptions (same positioning semantics as run_command).
        OutputOptions options = _codePodConfig.OutputOptions;

        // Preserve the first line (TotalLines) when withLineNumbers is enabled.
        if (wantsLineNumbers)
        {
            int newlineIdx = output.IndexOf('\n');
            string prefix = newlineIdx >= 0 ? output[..(newlineIdx + 1)] : output;
            string rest = newlineIdx >= 0 ? output[(newlineIdx + 1)..] : string.Empty;

            int prefixBytes = Encoding.UTF8.GetByteCount(prefix);
            int restBudget = Math.Max(1, options.MaxOutputBytes - prefixBytes);
            OutputOptions restOptions = new()
            {
                MaxOutputBytes = restBudget,
                Strategy = options.Strategy,
                TruncationMessage = options.TruncationMessage,
            };

            (string truncatedRest, _, _) = TruncateText(rest, restOptions);
            return Result.Ok(prefix + truncatedRest);
        }

        (string truncatedOutput, _, _) = TruncateText(output, options);
        return Result.Ok(truncatedOutput);
    }

    private static string BuildReadFileTruncationNote(int omittedBytes)
    {
        return $"\n(... {omittedBytes} bytes truncated)\n";
    }

    private static (string output, bool truncated, int omittedBytes) TruncateText(string output, OutputOptions options)
    {
        if (options.MaxOutputBytes <= 0)
        {
            return (output, false, 0);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(output ?? string.Empty);
        if (bytes.Length <= options.MaxOutputBytes)
        {
            return (output ?? string.Empty, false, 0);
        }

        int halfSize = options.MaxOutputBytes / 2;
        int omittedBytes = bytes.Length - options.MaxOutputBytes;
        string note = BuildReadFileTruncationNote(omittedBytes);

        return options.Strategy switch
        {
            TruncationStrategy.Head => (
                Encoding.UTF8.GetString(bytes, 0, options.MaxOutputBytes) +
                note,
                true,
                omittedBytes),

            TruncationStrategy.Tail => (
                note +
                Encoding.UTF8.GetString(bytes, bytes.Length - options.MaxOutputBytes, options.MaxOutputBytes),
                true,
                omittedBytes),

            TruncationStrategy.HeadAndTail => (
                Encoding.UTF8.GetString(bytes, 0, halfSize) +
                note +
                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize),
                true,
                omittedBytes),

            _ => (output ?? string.Empty, false, 0)
        };
    }

    [ToolFunction("Apply a unified diff patch to a file under /app")]
    private async Task<Result<string>> PatchFile(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        [ToolParam("Target file path under /app.")]
        [Required]
        string path,
        [ToolParam("Unified diff patch.")]
        [Required]
        string patch,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);
        state.UsedInThisTurn = true;

        string normalizedPath = PathSafety.NormalizeUnderWorkDir(path);

        byte[] originalBytes = await _docker.DownloadFileAsync(state.DbSession.ContainerId, normalizedPath, cancellationToken);
        string originalText = Encoding.UTF8.GetString(originalBytes);

        string patched = UnifiedDiffApplier.Apply(originalText, patch);
        byte[] newBytes = Encoding.UTF8.GetBytes(patched);

        await _docker.UploadFileAsync(state.DbSession.ContainerId, normalizedPath, newBytes, cancellationToken);
        await TouchSession(state.DbSession, cancellationToken);

        return Result.Ok($"Patched {normalizedPath} ({newBytes.Length} bytes)");
    }

    [ToolFunction("Download cloud files (from chat history) into /app")]
    private async Task<Result<string>> DownloadFiles(
        TurnContext ctx,
        [ToolParam("Session id.")]
        [Required]
        string sessionId,
        [ToolParam("Wildcard patterns matching cloud file names.")]
        [MinLength(1)]
        string[] patterns,
        CancellationToken cancellationToken)
    {
        TurnContext.SessionState state = await EnsureSession(ctx, sessionId, cancellationToken);
        state.UsedInThisTurn = true;

        List<string> patternsList = patterns.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (patternsList.Count == 0) return Result.Fail<string>("patterns is required");

        List<(int fileId, string fileName, DBFile file)> cloudFiles = CollectCloudFiles(ctx.MessageSteps);

        List<(int fileId, string fileName, string targetPath, int size)> downloaded = [];

        foreach ((int fileId, string fileName, DBFile file) in cloudFiles)
        {
            if (!patternsList.Any(p => WildcardMatcher.IsMatch(p, fileName))) continue;

            IFileService fs = _fileServiceFactory.Create(file.FileService);
            await using Stream s = await fs.Download(file.StorageKey, cancellationToken);
            using MemoryStream ms = new();
            await s.CopyToAsync(ms, cancellationToken);
            byte[] bytes = ms.ToArray();

            string safeName = PathSafety.SanitizeFileName(fileName);
            string targetPath = $"{PathSafety.WorkDir}/{fileId}_{safeName}";
            await _docker.UploadFileAsync(state.DbSession.ContainerId, targetPath, bytes, cancellationToken);

            downloaded.Add((fileId, fileName, targetPath, bytes.Length));
        }

        await TouchSession(state.DbSession, cancellationToken);

        if (downloaded.Count == 0)
        {
            return Result.Ok("No files matched the given patterns.");
        }

        StringBuilder sb = new();
        sb.AppendLine("Downloaded:");
        foreach (var d in downloaded.OrderBy(x => x.fileId))
        {
            sb.AppendLine($"- {d.fileName} (id={d.fileId}, {d.size} bytes) -> {d.targetPath}");
        }
        return Result.Ok(sb.ToString().TrimEnd());
    }

    private static List<(int fileId, string fileName, DBFile file)> CollectCloudFiles(IEnumerable<Step> steps)
    {
        Dictionary<int, (int, string, DBFile)> result = new();

        foreach (Step step in steps)
        {
            if (step.StepContents == null) continue;
            foreach (StepContent sc in step.StepContents)
            {
                DBFile? f = sc.StepContentFile?.File;
                if (f == null) continue;
                result[f.Id] = (f.Id, f.FileName, f);
            }
        }

        return result.Values.ToList();
    }
    
    private async Task<TurnContext.SessionState> EnsureSession(TurnContext ctx, string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("sessionId is required");
        }

        if (!ctx.SessionsBySessionId.TryGetValue(sessionId, out TurnContext.SessionState? state))
        {
            throw new InvalidOperationException($"Session not found in this turn: {sessionId}. Call create_docker_session first.");
        }

        if (!state.SnapshotTaken)
        {
            state.ArtifactsSnapshot = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);
            state.SnapshotTaken = true;
        }
        return state;
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

    private ResourceLimits MergeLimitsWithDefaults(ResourceLimitsArgs resourceLimitsArgs)
    {
        ResourceLimits defaults = _options.BuildDefaultResourceLimits();
        ResourceLimits merged = defaults.Clone();

        // null means use server default (not unlimited) per user request.
        if (resourceLimitsArgs.MemoryBytes != null) merged.MemoryBytes = resourceLimitsArgs.MemoryBytes.Value;
        if (resourceLimitsArgs.CpuCores != null) merged.CpuCores = resourceLimitsArgs.CpuCores.Value;
        if (resourceLimitsArgs.MaxProcesses != null) merged.MaxProcesses = resourceLimitsArgs.MaxProcesses.Value;

        // If config default is unlimited (0), keep it.
        return merged;
    }

    private async Task TouchSession(ChatDockerSession s, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        s.LastActiveAt = now;
        s.ExpiresAt = now.AddSeconds(_options.SessionIdleTimeoutSeconds);

        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        db.ChatDockerSessions.Attach(s);
        db.Entry(s).Property(x => x.LastActiveAt).IsModified = true;
        db.Entry(s).Property(x => x.ExpiresAt).IsModified = true;
        if (s.TerminatedAt != null)
        {
            db.Entry(s).Property(x => x.TerminatedAt).IsModified = true;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, FileEntry>> SnapshotArtifacts(string containerId, CancellationToken cancellationToken)
    {
        List<FileEntry> entries;
        try
        {
            entries = await _docker.ListDirectoryAsync(containerId, "/app/artifacts", cancellationToken);
        }
        catch
        {
            // artifacts dir may not exist yet.
            return new Dictionary<string, FileEntry>(StringComparer.Ordinal);
        }

        Dictionary<string, FileEntry> dict = new(StringComparer.Ordinal);
        foreach (FileEntry e in entries)
        {
            if (e.IsDirectory) continue;
            string path = e.Path.Replace('\\', '/');
            dict[path] = e;

            // Safety: cap tracking size.
            if (dict.Count >= _options.MaxArtifactsFilesToUpload) break;
        }

        return dict;
    }

    private async Task SyncArtifactsAfterToolCall(TurnContext ctx, TurnContext.SessionState state, CancellationToken cancellationToken)
    {
        // Compare artifacts snapshot and enqueue newly changed files as in-memory blobs.
        Dictionary<string, FileEntry> after = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);

        List<FileEntry> changed = [];
        foreach (FileEntry entry in after.Values)
        {
            if (!state.ArtifactsSnapshot.TryGetValue(entry.Path, out FileEntry? before))
            {
                changed.Add(entry);
                continue;
            }

            if (before.Size != entry.Size || before.LastModified != entry.LastModified)
            {
                changed.Add(entry);
            }
        }

        // Update snapshot so next tool calls are relative.
        state.ArtifactsSnapshot = after;

        if (changed.Count == 0) return;

        // Safety: cap tracked/uploaded artifacts per sync.
        if (changed.Count > _options.MaxArtifactsFilesToUpload) return;

        long total = state.PendingArtifactsBytesThisTurn;
        foreach (FileEntry file in changed)
        {
            if (file.IsDirectory) continue;
            if (state.PendingArtifactPaths.Contains(file.Path)) continue;

            if (_options.MaxSingleUploadBytes is long maxSingle && file.Size > maxSingle) continue;
            if (_options.MaxTotalUploadBytesPerTurn is long maxTotal && total > maxTotal) break;

            byte[] bytes = await _docker.DownloadFileAsync(state.DbSession.ContainerId, file.Path, cancellationToken);
            total += bytes.Length;

            string fileName = Path.GetFileName(file.Path);
            string contentType = GuessContentType(fileName);

            state.PendingArtifacts.Add(new PendingFileArtifact(file.Path, fileName, contentType, bytes));
            state.PendingArtifactPaths.Add(file.Path);
        }

        state.PendingArtifactsBytesThisTurn = total;
    }

    private static string GuessContentType(string fileName)
    {
        FileExtensionContentTypeProvider p = new();
        return p.TryGetContentType(fileName, out string? ct) ? ct! : "application/octet-stream";
    }

    private static NetworkMode ParseNetworkMode(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException("networkMode must be a string: none|bridge|host");
        }

        return v.Trim().ToLowerInvariant() switch
        {
            "none" => NetworkMode.None,
            "bridge" => NetworkMode.Bridge,
            "host" => NetworkMode.Host,
            _ => throw new InvalidOperationException($"Invalid networkMode '{v}'. Expected: none|bridge|host")
        };
    }

    private static string GenerateAlphaFirstToken(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        const string alphanum = letters + "0123456789";

        Span<char> buffer = stackalloc char[length];
        buffer[0] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
        for (int i = 1; i < length; i++)
        {
            buffer[i] = alphanum[RandomNumberGenerator.GetInt32(alphanum.Length)];
        }
        return new string(buffer);
    }
}
