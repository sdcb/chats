using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.DB.Extensions;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DBFile = Chats.DB.File;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterExecutor(
    IDockerService docker,
    IFileServiceFactory fileServiceFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<CodePodConfig> codePodConfig,
    IOptions<CodeInterpreterOptions> options,
    ILogger<CodeInterpreterExecutor> logger)
{
    private static readonly AttributedToolRegistry _toolRegistry = new(typeof(CodeInterpreterExecutor));

    private readonly IDockerService _docker = docker;
    private readonly IFileServiceFactory _fileServiceFactory = fileServiceFactory;
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
        string defaultMemoryBytesStr = FormatDefaultMemoryBytes(defaultLimits.MemoryBytes);
        string defaultCpuCoresStr = FormatDefaultCpuCores(defaultLimits.CpuCores);
        string defaultMaxProcessesStr = FormatDefaultMaxProcesses(defaultLimits.MaxProcesses);
        string networkModeStr = _options.GetDefaultNetworkMode().ToString().ToLowerInvariant();
        string allowedNetworkModesStr = _options.GetAllowedNetworkModesDisplay();
        string timeoutStr = _options.DefaultTimeoutSeconds?.ToString() ?? "unlimited";

        string defaultImageDisplay = _options.DefaultImage;
        if (!string.IsNullOrWhiteSpace(_options.DefaultImageDescription))
        {
            defaultImageDisplay = $"{defaultImageDisplay} ({_options.DefaultImageDescription})";
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = timeoutStr,
            ["{defaultMemoryBytes}"] = defaultMemoryBytesStr,
            ["{defaultCpuCores}"] = defaultCpuCoresStr,
            ["{defaultMaxProcesses}"] = defaultMaxProcessesStr,
            ["{defaultNetworkMode}"] = networkModeStr,
            ["{allowedNetworkModes}"] = allowedNetworkModesStr,
            ["{defaultImage}"] = defaultImageDisplay,
        };
    }

    private static string FormatDefaultMemoryBytes(long memoryBytes)
    {
        if (memoryBytes == 0) return "0 (unlimited)";
        return $"{memoryBytes.ToString(CultureInfo.InvariantCulture)} ({BytesFormatter.Format(memoryBytes)})";
    }

    private static string FormatDefaultCpuCores(double cpuCores)
    {
        if (cpuCores == 0) return "0 (unlimited)";
        return cpuCores.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatDefaultMaxProcesses(long maxProcesses)
    {
        if (maxProcesses == 0) return "0 (unlimited)";
        return maxProcesses.ToString(CultureInfo.InvariantCulture);
    }

    internal static string FormatResourceLimits(ResourceLimits limits)
    {
        List<string> parts = [];
        if (limits.MemoryBytes > 0)
        {
            parts.Add($"memory={BytesFormatter.Format(limits.MemoryBytes)}");
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

    public NeutralSystemMessage BuildSystemMessage(string? existingSystemPrompt)
    {
        StringBuilder sb = new();
        if (!string.IsNullOrWhiteSpace(existingSystemPrompt))
        {
            sb.AppendLine(existingSystemPrompt);
            sb.AppendLine();
        }

        sb.AppendLine("You have access to sandboxed code interpreter environments.");
        sb.AppendLine($"- Working directory: {codePodConfig.Value.WorkDir}");
        sb.AppendLine($"- Artifacts directory: {codePodConfig.Value.WorkDir}/{codePodConfig.Value.ArtifactsDir}, MUST copy to this folder so user can download it!");
        sb.AppendLine("- Call create_docker_session to get a sessionId.");

        return NeutralSystemMessage.FromText(sb.ToString());
    }

    public string? BuildCloudFilesContextPrefix(IEnumerable<Step> messageSteps)
    {
        List<DBFile> cloudFiles = CollectCloudFiles(messageSteps);

        if (cloudFiles.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new();
        sb.AppendLine("[Cloud Files Available]");
        foreach (DBFile file in cloudFiles)
        {
            sb.AppendLine($"- {ToAIModelReadable(file)}");
        }
        sb.AppendLine("Use download_chat_files with wildcard patterns matching the file names above.");
        return sb.ToString();
    }

    public string? BuildCodeInterpreterContextPrefix(IEnumerable<ChatTurn> messageTurns)
        => BuildCodeInterpreterContextPrefix(messageTurns, DateTime.UtcNow);

    internal static string? BuildCodeInterpreterContextPrefix(IEnumerable<ChatTurn> messageTurns, DateTime utcNow)
    {
        List<DBFile> cloudFiles = CollectCloudFiles(messageTurns.SelectMany(t => t.Steps));
        List<ChatDockerSession> activeSessions = CollectActiveSessions(messageTurns, utcNow);

        if (cloudFiles.Count == 0 && activeSessions.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new();

        if (cloudFiles.Count > 0)
        {
            sb.AppendLine("[Cloud Files Available]");
            foreach (DBFile file in cloudFiles)
            {
                sb.AppendLine($"- {ToAIModelReadable(file)}");
            }
            sb.AppendLine("Use download_chat_files with wildcard patterns matching the file names above.");
            sb.AppendLine();
        }

        if (activeSessions.Count > 0)
        {
            sb.AppendLine("[Active Docker Sessions]");
            foreach (ChatDockerSession s in activeSessions)
            {
                sb.AppendLine($"- {s.AIReableDockerInfo}");
            }
            sb.AppendLine("Use the sessionId above when calling code interpreter tools.");
        }

        return sb.ToString().TrimEnd();
    }

    static string ToAIModelReadable(DBFile file)
    {
        return $"{file.FileName} (size:{HumanizeFileSize(file.Size)}{ImageRelated(file)})";

        static string ImageRelated(DBFile file)
        {
            if (file.FileImageInfo != null)
            {
                return $", resolution: {file.FileImageInfo.Width}x{file.FileImageInfo.Height}";
            }
            return "";
        }
    }

    internal static string HumanizeFileSize(long fileSize)
    {
        if (fileSize >= 1024L * 1024 * 1024)
            return $"{fileSize / (1024.0 * 1024 * 1024):0.##}GB";
        if (fileSize >= 1024L * 1024)
            return $"{fileSize / (1024.0 * 1024):0.##}MB";
        if (fileSize >= 1024L)
            return $"{fileSize / 1024.0:0.##}KB";
        return $"{fileSize}B";
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
            public required string[] ShellPrefix { get; init; }
            public Dictionary<string, FileEntry> ArtifactsSnapshot { get; set; } = new(StringComparer.Ordinal);
            public bool SnapshotTaken { get; set; }
            public bool UsedInThisTurn { get; set; }
            public List<PendingFileArtifact> PendingArtifacts { get; } = [];
            public HashSet<string> PendingArtifactPaths { get; } = new(StringComparer.Ordinal);
            public long PendingArtifactsBytesThisTurn { get; set; }
        }
    }

    private static string[] ParseShellPrefixCsv(string? csv, bool isWindowsContainer)
    {
        if (!string.IsNullOrWhiteSpace(csv))
        {
            string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                return parts;
            }
        }

        // Fallback for legacy rows (or misconfigured data): use bootstrap shells.
        return isWindowsContainer ? ["cmd", "/c"] : ["/bin/sh", "-lc"];
    }

    private static string ToShellPrefixCsv(string[] shellPrefix)
    {
        if (shellPrefix == null || shellPrefix.Length == 0)
        {
            throw new InvalidOperationException("ShellPrefix is required");
        }
        return string.Join(',', shellPrefix);
    }

    public async IAsyncEnumerable<ToolProgressDelta> ExecuteToolCallAsync(
        TurnContext ctx,
        string toolCallId,
        string toolName,
        string rawJsonArgs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AttributedToolRegistry.ToolInvokeResult? inv = null;
        ToolProgressDelta? invokeFailure = null;
        try
        {
            inv = _toolRegistry.Invoke(this, ctx, toolName, rawJsonArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CodeInterpreter tool failed: {toolName}", toolName);
            invokeFailure = new ToolCompletedToolProgressDelta { Result = Result.Fail<string>(ex.Message) };
        }

        if (invokeFailure != null)
        {
            yield return invokeFailure;
            yield break;
        }

        switch (inv)
        {
            case AttributedToolRegistry.ToolInvokeStream s:
                {
                    Exception? streamException = null;
                    ConfiguredCancelableAsyncEnumerable<ToolProgressDelta>.Enumerator enumerator = s.Stream
                        .WithCancellation(cancellationToken)
                        .GetAsyncEnumerator();
                    try
                    {
                        while (true)
                        {
                            bool moved;
                            try
                            {
                                moved = await enumerator.MoveNextAsync();
                            }
                            catch (Exception ex)
                            {
                                streamException = ex;
                                break;
                            }

                            if (!moved)
                            {
                                break;
                            }

                            yield return enumerator.Current;
                        }
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
                    }

                    if (streamException != null)
                    {
                        _logger.LogWarning(streamException, "CodeInterpreter tool stream failed: {toolName}", toolName);
                        yield return new ToolCompletedToolProgressDelta { Result = Result.Fail<string>(streamException.Message) };
                    }
                    break;
                }

            case AttributedToolRegistry.ToolInvokeTask t:
                {
                    Result<string> r;
                    try
                    {
                        r = await t.Task;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "CodeInterpreter tool task failed: {toolName}", toolName);
                        r = Result.Fail<string>(ex.Message);
                    }
                    yield return new ToolCompletedToolProgressDelta { Result = r };
                    break;
                }

            default:
                yield return new ToolCompletedToolProgressDelta
                {
                    Result = Result.Fail<string>($"Unknown tool invocation result type: {inv?.GetType().Name}")
                };
                break;
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

    [ToolFunction("Create a docker session")]
    internal async IAsyncEnumerable<ToolProgressDelta> CreateDockerSession(
        TurnContext ctx,
        [ToolParam("Docker image to use (null means use server default: {defaultImage}).")]
        string? image,
        [ToolParam("Label. If empty, server will use the new container id prefix (first 12 chars).")]
        string? label,
        [ToolParam("Memory limit in bytes (null means use server default: {defaultMemoryBytes}). 0 means unlimited.")]
        long? memoryBytes,
        [ToolParam("CPU limit in cores (null means use server default: {defaultCpuCores}). 0 means unlimited.")]
        double? cpuCores,
        [ToolParam("Max processes (null means use server default: {defaultMaxProcesses}). 0 means unlimited.")]
        long? maxProcesses,
        [ToolParam("Network mode. One of: {allowedNetworkModes}. null means use server default: {defaultNetworkMode}.")]
        [EnumDataType(typeof(NetworkMode))]
        string? networkMode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hasLabel = !string.IsNullOrWhiteSpace(label);

        // Reuse from in-memory first (only possible when label provided).
        if (hasLabel && ctx.SessionsBySessionId.TryGetValue(label!, out TurnContext.SessionState? existing))
        {
            existing.UsedInThisTurn = true;
            yield return new ToolCompletedToolProgressDelta { Result = Result.Ok(existing.DbSession.AIReableDockerInfo) };
            yield break;
        }

        // Resolve from DB along the ParentId chain of the current generating turn.
        DateTime nowUtc = DateTime.UtcNow;
        ChatDockerSession? dbSession = null;
        if (hasLabel)
        {
            dbSession = ctx.MessageTurns
                .SelectMany(t => t.ChatDockerSessions)
                .LastOrDefault(s => s.TerminatedAt == null && s.ExpiresAt > nowUtc && s.Label == label);
        }

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

            NetworkMode maxAllowedNetworkMode = _options.GetMaxAllowedNetworkMode();
            if ((int)effectiveNetworkMode > (int)maxAllowedNetworkMode)
            {
                string allowed = _options.GetAllowedNetworkModesDisplay();
                yield return new ToolCompletedToolProgressDelta
                {
                    Result = Result.Fail<string>(
                        $"Requested networkMode '{effectiveNetworkMode.ToString().ToLowerInvariant()}' exceeds MaxAllowedNetworkMode " +
                        $"'{maxAllowedNetworkMode.ToString().ToLowerInvariant()}'. Allowed: {allowed}.")
                };
                yield break;
            }

            if (memoryBytes != null || cpuCores != null || maxProcesses != null)
            {
                limits = MergeLimitsWithDefaults(memoryBytes, cpuCores, maxProcesses);
                limits.Validate(max);
            }

            string effectiveImage = string.IsNullOrWhiteSpace(image) ? _options.DefaultImage : image;

            // 流式输出镜像拉取进度
            await foreach (CommandOutputEvent ev in _docker.EnsureImageAsync(effectiveImage, cancellationToken))
            {
                switch (ev)
                {
                    case CommandStdoutEvent o:
                        yield return new StdOutToolProgressDelta { StdOutput = o.Data };
                        break;
                    case CommandStderrEvent e:
                        yield return new StdErrorToolProgressDelta { StdError = e.Data };
                        break;
                }
            }

            ContainerInfo container = await _docker.CreateContainerCoreAsync(effectiveImage, limits, effectiveNetworkMode, cancellationToken);

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
                OwnerChatId = ctx.CurrentAssistantTurn.ChatId,
                Label = label!,
                ContainerId = container.ContainerId,
                Image = effectiveImage,
                ShellPrefix = ToShellPrefixCsv(container.ShellPrefix),
                Ip = container.Ip,
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
            ShellPrefix = ParseShellPrefixCsv(dbSession.ShellPrefix, _codePodConfig.IsWindowsContainer),
            UsedInThisTurn = true,
        };
        ctx.SessionsBySessionId[label!] = state;

        if (!state.SnapshotTaken)
        {
            state.ArtifactsSnapshot = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);
            state.SnapshotTaken = true;
        }

        string info = dbSession.AIReableDockerInfo;
        try
        {
            // Try to read skills.md for the AI model (only for newly created/loaded sessions)
            byte[] skillsBytes = await _docker.DownloadFileAsync(dbSession.ContainerId, $"{_codePodConfig.WorkDir}/skills.md", cancellationToken);
            string skillsContent = Encoding.UTF8.GetString(skillsBytes);
            if (!string.IsNullOrWhiteSpace(skillsContent))
            {
                info += $"\n{skillsContent}";
            }
        }
        catch
        {
            // skills.md may not exist, ignore
        }

        yield return new ToolCompletedToolProgressDelta { Result = Result.Ok(info) };
    }

    [ToolFunction("Destroy the docker session")]
    internal async Task<Result<string>> DestroySession(
        TurnContext ctx,
        [Required]
        string sessionId,
        CancellationToken cancellationToken)
    {
        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            return Result.Fail<string>(ensureResult.Error!);
        }
        TurnContext.SessionState state = ensureResult.Value!;

        try
        {
            await _docker.DeleteContainerAsync(state.DbSession.ContainerId, cancellationToken);
        }
        catch
        {
            // best-effort
        }

        state.DbSession.TerminatedAt = DateTime.UtcNow;
        await TerminateSession(state.DbSession.Id, cancellationToken);

        ctx.SessionsBySessionId.Remove(sessionId);

        return Result.Ok($"Destroyed session: {sessionId}");
    }

    [ToolFunction("Run a shell command inside the session workdir /app")]
    internal async IAsyncEnumerable<ToolProgressDelta> RunCommand(
        TurnContext ctx,
        [Required]
        string sessionId,
        [ToolParam("Shell command to run")]
        [Required]
        string command,
        [ToolParam("Command timeout seconds (null means use server default).")]
        int? timeoutSeconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            yield return new ToolCompletedToolProgressDelta { Result = Result.Fail<string>(ensureResult.Error!) };
            yield break;
        }
        TurnContext.SessionState state = ensureResult.Value!;
        state.UsedInThisTurn = true;
        int timeout = _options.GetEffectiveTimeoutSeconds(timeoutSeconds);

        CommandExitEvent? exit = null;

        Exception? streamException = null;
        ConfiguredCancelableAsyncEnumerable<CommandOutputEvent>.Enumerator enumerator = _docker
            .ExecuteCommandStreamAsync(state.DbSession.ContainerId, state.ShellPrefix, command, _codePodConfig.WorkDir, timeout, cancellationToken)
            .WithCancellation(cancellationToken)
            .GetAsyncEnumerator();
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    streamException = ex;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                CommandOutputEvent ev = enumerator.Current;
                switch (ev)
                {
                    case CommandStdoutEvent o:
                        yield return new StdOutToolProgressDelta { StdOutput = o.Data };
                        break;
                    case CommandStderrEvent e:
                        yield return new StdErrorToolProgressDelta { StdError = e.Data };
                        break;
                    case CommandExitEvent x:
                        exit = x;
                        break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (streamException != null)
        {
            yield return new ToolCompletedToolProgressDelta { Result = Result.Fail<string>(streamException.Message) };
            yield break;
        }

        if (exit == null)
        {
            yield return new ToolCompletedToolProgressDelta
            {
                Result = Result.Fail<string>("Command stream ended without exit event")
            };
            yield break;
        }

        // DockerService 已保证 exit 事件与 ExecuteCommandAsync 返回完全一致（包含 truncate/IsTruncated）。
        // RunCommand 只负责把 CommandExitEvent 格式化为旧 run_command 的文本输出。
        string output = CommandExitEventFormatter.FormatForRunCommand(exit);

        await TouchSession(state.DbSession.Id, cancellationToken);
        await SyncArtifactsAfterToolCall(ctx, state, cancellationToken);

        yield return new ToolCompletedToolProgressDelta
        {
            Result = exit.ExitCode == 0 ? Result.Ok(output) : Result.Fail<string>(output)
        };
    }

    [ToolFunction("Write a file under /app")]
    internal async Task<Result<string>> WriteFile(
        TurnContext ctx,
        [Required]
        string sessionId,
        [ToolParam("Path under /app."), Required]
        string path,
        [ToolParam("Text content(written as UTF-8)"), Required]
        string text,
        CancellationToken cancellationToken)
    {
        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            return Result.Fail<string>(ensureResult.Error!);
        }
        TurnContext.SessionState state = ensureResult.Value!;
        state.UsedInThisTurn = true;

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await _docker.UploadFileAsync(state.DbSession.ContainerId, path, bytes, cancellationToken);
        await TouchSession(state.DbSession.Id, cancellationToken);

        await SyncArtifactsAfterToolCall(ctx, state, cancellationToken);

        int lineCount = string.IsNullOrEmpty(text) ? 0 : text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;
        return Result.Ok($"Wrote {lineCount} lines to {path}");
    }

    [ToolFunction("Read a file under /app")]
    internal async Task<Result<string>> ReadFile(
        TurnContext ctx,
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
        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            return Result.Fail<string>(ensureResult.Error!);
        }
        TurnContext.SessionState state = ensureResult.Value!;
        state.UsedInThisTurn = true;

        byte[] bytes = await _docker.DownloadFileAsync(state.DbSession.ContainerId, path, cancellationToken);

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
            string headerTemplate = $"{prefix}Path: {path}\nSize: {bytes.Length}\nBase64(first {{0}} bytes):\n";

            int budget = Math.Max(1, binaryOptions.MaxOutputBytes);
            int minHeaderBytes = Encoding.UTF8.GetByteCount(string.Format(headerTemplate, 0));
            int availableForBase64Chars = Math.Max(0, budget - minHeaderBytes);

            // base64 chars are ASCII; chars == bytes in UTF-8 for this portion.
            int maxRawBytesFromBase64 = availableForBase64Chars / 4 * 3;
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

        await TouchSession(state.DbSession.Id, cancellationToken);

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

    private static (string output, bool truncated, int omittedLines) TruncateText(string output, OutputOptions options)
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
        
        string truncatedOutput;
        switch (options.Strategy)
        {
            case TruncationStrategy.Head:
                truncatedOutput = Encoding.UTF8.GetString(bytes, 0, options.MaxOutputBytes);
                break;
            case TruncationStrategy.Tail:
                truncatedOutput = Encoding.UTF8.GetString(bytes, bytes.Length - options.MaxOutputBytes, options.MaxOutputBytes);
                break;
            case TruncationStrategy.HeadAndTail:
                truncatedOutput = Encoding.UTF8.GetString(bytes, 0, halfSize) +
                                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize);
                break;
            default:
                return (output ?? string.Empty, false, 0);
        }

        // Calculate omitted lines
        int totalLines = string.IsNullOrEmpty(output) ? 0 : output.Split(["\r\n", "\n"], StringSplitOptions.None).Length;
        int keptLines = string.IsNullOrEmpty(truncatedOutput) ? 0 : truncatedOutput.Split(["\r\n", "\n"], StringSplitOptions.None).Length;
        int omittedLines = Math.Max(0, totalLines - keptLines);
        
        string note = string.Format(options.TruncationMessage, omittedLines);

        return options.Strategy switch
        {
            TruncationStrategy.Head => (
                Encoding.UTF8.GetString(bytes, 0, options.MaxOutputBytes) +
                note,
                true,
                omittedLines),

            TruncationStrategy.Tail => (
                note +
                Encoding.UTF8.GetString(bytes, bytes.Length - options.MaxOutputBytes, options.MaxOutputBytes),
                true,
                omittedLines),

            TruncationStrategy.HeadAndTail => (
                Encoding.UTF8.GetString(bytes, 0, halfSize) +
                note +
                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize),
                true,
                omittedLines),

            _ => (output ?? string.Empty, false, 0)
        };
    }

    [ToolFunction("""
        Apply a patch to a file under /app.
        The target file is specified by the 'path' argument.
        The 'patch' argument MUST be unified-diff hunks ONLY (no headers/wrappers).
        """)]
    internal async Task<Result<string>> PatchFile(
        TurnContext ctx,
        [Required] string sessionId,
        [ToolParam("Target file path under /app."), Required] string path,
        [ToolParam("""
            Patch text (RAW, no markdown). MUST contain unified-diff hunks ONLY.

            Supported input:
            - One or more hunks.
            - Each hunk header MUST be exactly:
                @@ -oldStart,oldCount +newStart,newCount @@
                (full ranges required)
            - Inside hunks, each line must start with:
                - ' ' (context)
                - '+' (add)
                - '-' (delete)
                - or be exactly: \\ No newline at end of file

            Not supported (do NOT include):
            - diff --git / index / --- / +++
            - *** Begin Patch/*** End Patch wrappers
            - markdown code fences (```)
            - any extra commentary text

            Notes:
            - An empty context line must be represented as a single space ' ' line (no empty lines inside hunks).
            - Recommended workflow: call read_file(withLineNumbers=true) first, then generate hunks with enough context.
            """), Required] string patch,
        CancellationToken cancellationToken)
    {
        if (!UnifiedDiffPatchToolValidator.TryValidate(patch, out string validationError))
        {
            return Result.Fail<string>(validationError);
        }

        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            return Result.Fail<string>(ensureResult.Error!);
        }
        TurnContext.SessionState state = ensureResult.Value!;
        state.UsedInThisTurn = true;

        byte[] originalBytes = await _docker.DownloadFileAsync(state.DbSession.ContainerId, path, cancellationToken);
        string originalText = Encoding.UTF8.GetString(originalBytes);

        string patched = UnifiedDiffApplier.Apply(originalText, patch);
        byte[] newBytes = Encoding.UTF8.GetBytes(patched);

        await _docker.UploadFileAsync(state.DbSession.ContainerId, path, newBytes, cancellationToken);
        await TouchSession(state.DbSession.Id, cancellationToken);

        return Result.Ok($"Patched {path} ({newBytes.Length} bytes)");
    }

    [ToolFunction("Download cloud files (from chat history) into /app")]
    internal async Task<Result<string>> DownloadChatFiles(
        TurnContext ctx,
        [Required]
        string sessionId,
        [ToolParam("Wildcard patterns matching cloud file names.")]
        [MinLength(1)]
        string[] patterns,
        CancellationToken cancellationToken)
    {
        Result<TurnContext.SessionState> ensureResult = await EnsureSession(ctx, sessionId, cancellationToken);
        if (!ensureResult.IsSuccess)
        {
            return Result.Fail<string>(ensureResult.Error!);
        }
        TurnContext.SessionState state = ensureResult.Value!;
        state.UsedInThisTurn = true;

        List<string> patternsList = patterns.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (patternsList.Count == 0) return Result.Fail<string>("patterns is required");

        List<DBFile> cloudFiles = CollectCloudFiles(ctx.MessageSteps);
        List<DBFile> downloaded = [];

        foreach (DBFile file in cloudFiles)
        {
            if (!patternsList.Any(p => WildcardMatcher.IsMatch(p, file.FileName))) continue;

            IFileService fs = _fileServiceFactory.Create(file.FileService);
            await using Stream s = await fs.Download(file.StorageKey, cancellationToken);
            using MemoryStream ms = new();
            await s.CopyToAsync(ms, cancellationToken);
            byte[] bytes = ms.ToArray();

            string targetPath = $"{_codePodConfig.WorkDir}/{file.FileName}";
            await _docker.UploadFileAsync(state.DbSession.ContainerId, targetPath, bytes, cancellationToken);

            downloaded.Add(file);
        }

        await TouchSession(state.DbSession.Id, cancellationToken);

        if (downloaded.Count == 0)
        {
            return Result.Ok("No files matched the given patterns.");
        }

        StringBuilder sb = new();
        sb.AppendLine("Downloaded:");
        foreach (DBFile file in downloaded)
        {
            sb.AppendLine($"- {ToAIModelReadable(file)}");
        }
        return Result.Ok(sb.ToString().TrimEnd());
    }

    internal static List<DBFile> CollectCloudFiles(IEnumerable<Step> steps)
    {
        Dictionary<string, DBFile> result = [];

        foreach (Step step in steps)
        {
            if (step.StepContents == null) continue;
            foreach (StepContent sc in step.StepContents)
            {
                DBFile? f = sc.StepContentFile?.File;
                if (f == null) continue;
                result[f.FileName] = f;
            }
        }

        return [.. result.Values];
    }

    internal static List<ChatDockerSession> CollectActiveSessions(IEnumerable<ChatTurn> turns, DateTime utcNow)
    {
        Dictionary<string, ChatDockerSession> byLabel = new(StringComparer.Ordinal);

        foreach (ChatDockerSession s in turns.SelectMany(t => t.ChatDockerSessions))
        {
            if (string.IsNullOrWhiteSpace(s.Label)) continue;
            if (s.TerminatedAt != null) continue;
            if (s.ExpiresAt <= utcNow) continue;

            // keep the last one by traversal order
            byLabel[s.Label] = s;
        }

        return [.. byLabel.Values];
    }

    private async Task<Result<TurnContext.SessionState>> EnsureSession(TurnContext ctx, string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result.Fail<TurnContext.SessionState>("sessionId is required");
        }

        if (!ctx.SessionsBySessionId.TryGetValue(sessionId, out TurnContext.SessionState? state))
        {
            // First, check if session exists (regardless of state)
            ChatDockerSession? dbSession = ctx.MessageTurns
                .SelectMany(t => t.ChatDockerSessions)
                .LastOrDefault(s => s.Label == sessionId);

            if (dbSession == null)
            {
                return Result.Fail<TurnContext.SessionState>($"Session not found in this turn: {sessionId}. Call create_docker_session first.");
            }

            DateTime nowUtc = DateTime.UtcNow;

            // Check if session was destroyed
            if (dbSession.TerminatedAt != null)
            {
                return Result.Fail<TurnContext.SessionState>($"Session '{sessionId}' was destroyed at {dbSession.TerminatedAt:yyyy-MM-dd HH:mm:ss} UTC. Create a new session.");
            }

            // Check if session has expired
            if (dbSession.ExpiresAt <= nowUtc)
            {
                return Result.Fail<TurnContext.SessionState>($"Session '{sessionId}' expired at {dbSession.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC. Create a new session.");
            }

            // Session is active
            state = new TurnContext.SessionState()
            {
                DbSession = dbSession,
                ShellPrefix = ParseShellPrefixCsv(dbSession.ShellPrefix, _codePodConfig.IsWindowsContainer),
                UsedInThisTurn = true,
            };
            state.ArtifactsSnapshot = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);
            state.SnapshotTaken = true;
            ctx.SessionsBySessionId[sessionId] = state;
        }

        if (!state.SnapshotTaken)
        {
            state.ArtifactsSnapshot = await SnapshotArtifacts(state.DbSession.ContainerId, cancellationToken);
            state.SnapshotTaken = true;
        }
        return Result.Ok(state);
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

    private ResourceLimits MergeLimitsWithDefaults(long? memoryBytes, double? cpuCores, long? maxProcesses)
    {
        ResourceLimits defaults = _options.BuildDefaultResourceLimits();
        ResourceLimits merged = defaults.Clone();

        // null means use server default (not unlimited) per user request.
        if (memoryBytes != null) merged.MemoryBytes = memoryBytes.Value;
        if (cpuCores != null) merged.CpuCores = cpuCores.Value;
        if (maxProcesses != null) merged.MaxProcesses = maxProcesses.Value;

        // If config default is unlimited (0), keep it.
        return merged;
    }

    private async Task TouchSession(long dockerSessionId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        // In-Memory provider doesn't support ExecuteUpdateAsync, use fallback
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            ChatDockerSession? session = await db.ChatDockerSessions
                .FirstOrDefaultAsync(x => x.Id == dockerSessionId, cancellationToken);
            if (session != null)
            {
                session.LastActiveAt = now;
                session.ExpiresAt = now.AddSeconds(_options.SessionIdleTimeoutSeconds);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            await db.ChatDockerSessions
                .Where(x => x.Id == dockerSessionId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(v => v.LastActiveAt, now)
                    .SetProperty(v => v.ExpiresAt, now.AddSeconds(_options.SessionIdleTimeoutSeconds)), cancellationToken);
        }
    }

    private async Task TerminateSession(long dockerSessionId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        // In-Memory provider doesn't support ExecuteUpdateAsync, use fallback
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            ChatDockerSession? session = await db.ChatDockerSessions
                .FirstOrDefaultAsync(x => x.Id == dockerSessionId, cancellationToken);
            if (session != null)
            {
                session.TerminatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            await db.ChatDockerSessions
                .Where(x => x.Id == dockerSessionId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(v => v.TerminatedAt, now), cancellationToken);
        }
    }

    private async Task<Dictionary<string, FileEntry>> SnapshotArtifacts(string containerId, CancellationToken cancellationToken)
    {
        List<FileEntry> entries;
        try
        {
            entries = await _docker.ListDirectoryAsync(containerId, $"{_codePodConfig.WorkDir}/{_codePodConfig.ArtifactsDir}", cancellationToken);
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
