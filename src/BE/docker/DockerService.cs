using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Chats.DockerInterface.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Writers;
using SdkContainerStatus = Chats.DockerInterface.Models.ContainerStatus;

namespace Chats.DockerInterface;

/// <summary>
/// Docker服务实现
/// </summary>
public class DockerService(CodePodConfig config, ILogger<DockerService>? logger = null) : IDockerService
{
    private readonly DockerClient _client = new DockerClientConfiguration(config.GetDockerEndpointUri()).CreateClient();

    /// <inheritdoc />
    public CodePodConfig Config => config;

    public async IAsyncEnumerable<CommandOutputEvent> EnsureImageAsync(string image, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 检查镜像是否存在
        bool imageExists = false;
        try
        {
            await _client.Images.InspectImageAsync(image, cancellationToken);
            imageExists = true;
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            imageExists = false;
        }

        if (imageExists)
        {
            yield return new CommandStdoutEvent($"Image {image} already exists\n");
            yield break;
        }

        // 镜像不存在，需要拉取
        yield return new CommandStdoutEvent($"Pulling image {image}...\n");

        // 使用 Channel 来桥接 Progress 回调和 IAsyncEnumerable
        System.Threading.Channels.Channel<string> channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        Task pullTask = _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image },
            null,
            new Progress<JSONMessage>(m =>
            {
                if (!string.IsNullOrEmpty(m.Status))
                {
                    string message = FormatPullProgressMessage(m);
                    channel.Writer.TryWrite(message);
                }
            }),
            cancellationToken);

        // 启动一个任务在 pull 完成后关闭 channel
        _ = pullTask.ContinueWith(_ => channel.Writer.Complete(), TaskScheduler.Default);

        await foreach (string message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new CommandStdoutEvent(message);
        }

        await pullTask; // 确保异常被传播
        yield return new CommandStdoutEvent($"Image {image} pulled successfully\n");
    }

    private static string FormatPullProgressMessage(JSONMessage m)
    {
        StringBuilder sb = new();

        // 添加层 ID 前缀（如果存在）
        if (!string.IsNullOrEmpty(m.ID))
        {
            sb.Append(m.ID);
            sb.Append(": ");
        }

        sb.Append(m.Status);

        // 如果有进度信息，显示下载/提取进度
        if (m.Progress != null && m.Progress.Total > 0 && m.Progress.Current > 0)
        {
            double percentage = (double)m.Progress.Current / m.Progress.Total * 100;
            string current = BytesFormatter.Format(m.Progress.Current);
            string total = BytesFormatter.Format(m.Progress.Total);
            sb.Append($" {current}/{total} ({percentage:F0}%)");
        }

        sb.Append('\n');
        return sb.ToString();
    }

    public async Task<List<string>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        IList<ImagesListResponse> images = await _client.Images.ListImagesAsync(
            new ImagesListParameters { All = true },
            cancellationToken);

        HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);
        foreach (ImagesListResponse img in images)
        {
            if (img.RepoTags == null) continue;
            foreach (string tag in img.RepoTags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;
                if (tag == "<none>:<none>") continue;
                tags.Add(tag);
            }
        }

        return tags
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ContainerInfo> CreateContainerCoreAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
    {
        // 使用指定的资源限制或默认值
        ResourceLimits limits = resourceLimits ?? config.DefaultResourceLimits;
        // 验证不超过最大限制
        limits.Validate(config.MaxResourceLimits);

        // 使用指定的网络模式或默认值
        NetworkMode network = networkMode ?? config.DefaultNetworkMode;

        string containerName = $"{config.LabelPrefix}-{Guid.NewGuid():N}";
        Dictionary<string, string> labels = new()
        {
            [$"{config.LabelPrefix}.managed"] = "true",
            [$"{config.LabelPrefix}.created"] = DateTimeOffset.UtcNow.ToString("o"),
            [$"{config.LabelPrefix}.memory"] = limits.MemoryBytes.ToString(),
            [$"{config.LabelPrefix}.cpu"] = limits.CpuCores.ToString("F2"),
            [$"{config.LabelPrefix}.pids"] = limits.MaxProcesses.ToString(),
            [$"{config.LabelPrefix}.network"] = network.ToString().ToLower()
        };

        // 构建 HostConfig，Windows 容器不支持某些选项
        HostConfig hostConfig = new()
        {
            NetworkMode = network.ToDockerNetworkMode(config.IsWindowsContainer),
            Memory = limits.MemoryBytes,
            NanoCPUs = (long)(limits.CpuCores * 1_000_000_000) // 1e9 = 1 CPU
        };

        if (!config.IsWindowsContainer)
        {
            // Linux 容器：支持 PidsLimit
            hostConfig.PidsLimit = limits.MaxProcesses;
        }
        else
        {
            // Windows 容器：Windows Server 2025 支持 Memory 和 CPU 限制
            // 但不支持 PidsLimit（这是 Linux cgroups 特有功能）
            logger?.LogDebug("Windows container mode: PidsLimit is not supported");
        }

        CreateContainerResponse response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = containerName,
            Image = image,
            Tty = false,
            AttachStdout = false,
            AttachStderr = false,
            Cmd = config.GetKeepAliveCommand(),
            WorkingDir = config.WorkDir,
            Labels = labels,
            HostConfig = hostConfig
        }, cancellationToken);

        await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancellationToken);

        logger?.LogInformation("Created and started container {ContainerId} (name: {Name}, memory: {Memory}MB, cpu: {Cpu}, pids: {Pids}, network: {Network})",
            response.ID[..12], containerName,
            limits.MemoryBytes / 1024 / 1024, limits.CpuCores, limits.MaxProcesses, network);

        // 创建工作目录和 artifacts 目录
        string mkdirCmd = config.GetMkdirCommand(config.WorkDir, $"{config.WorkDir}/{config.ArtifactsDir}");
        string[] bootstrapPrefix = config.IsWindowsContainer ? ["cmd", "/c"] : ["/bin/sh", "-lc"];
        await ExecuteCommandAsync(response.ID, [.. bootstrapPrefix, mkdirCmd], "/", 30, cancellationToken);

        // 探测容器内可用的 shell 前缀（用于后续命令执行；需要落库）
        string[] shellPrefix = await DetectShellPrefixAsync(response.ID, cancellationToken);
        logger?.LogDebug("Detected shell prefix for container {ContainerId}: {ShellPrefix}", response.ID[..12], string.Join(' ', shellPrefix));

        ContainerInspectResponse inspect = await _client.Containers.InspectContainerAsync(response.ID, cancellationToken);
        string? ip = ExtractContainerIp(inspect.NetworkSettings);

        return new ContainerInfo
        {
            ContainerId = response.ID,
            Name = containerName,
            Image = image,
            DockerStatus = "running",
            Status = SdkContainerStatus.Warming,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            Labels = labels,
            ShellPrefix = shellPrefix,
            Ip = ip
        };
    }

    public async Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default)
    {
        IList<ContainerListResponse> containers = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool>
                {
                    [$"{config.LabelPrefix}.managed=true"] = true
                }
            }
        }, cancellationToken);

        List<ContainerInfo> result = new();
        foreach (ContainerListResponse? container in containers)
        {
            result.Add(new ContainerInfo
            {
                ContainerId = container.ID,
                Name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID[..12],
                Image = container.Image,
                DockerStatus = container.State,
                Status = SdkContainerStatus.Idle,
                CreatedAt = container.Created,
                Labels = new Dictionary<string, string>(container.Labels),
                ShellPrefix = config.IsWindowsContainer ? ["cmd", "/c"] : ["/bin/sh", "-lc"],
                Ip = ExtractContainerIp(container.NetworkSettings?.Networks)
            });
        }

        return result;
    }

    public async Task<ContainerInfo?> GetContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        try
        {
            ContainerInspectResponse inspect = await _client.Containers.InspectContainerAsync(containerId, cancellationToken);

            if (!inspect.Config.Labels.TryGetValue($"{config.LabelPrefix}.managed", out string? managed) || managed != "true")
            {
                return null;
            }

            string? ip = ExtractContainerIp(inspect.NetworkSettings);

            return new ContainerInfo
            {
                ContainerId = inspect.ID,
                Name = inspect.Name.TrimStart('/'),
                Image = inspect.Config.Image,
                DockerStatus = inspect.State.Status,
                Status = SdkContainerStatus.Idle,
                CreatedAt = inspect.Created,
                StartedAt = DateTimeOffset.TryParse(inspect.State.StartedAt, out DateTimeOffset started) ? started : null,
                Labels = new Dictionary<string, string>(inspect.Config.Labels),
                ShellPrefix = config.IsWindowsContainer ? ["cmd", "/c"] : ["/bin/sh", "-lc"],
                Ip = ip
            };
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Force=true 会自动停止运行中的容器
            await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, cancellationToken);
            logger?.LogInformation("Deleted container {ContainerId}", containerId[..Math.Min(12, containerId.Length)]);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger?.LogWarning("Container {ContainerId} not found, already removed", containerId[..Math.Min(12, containerId.Length)]);
        }
    }

    public async Task DeleteAllManagedContainersAsync(CancellationToken cancellationToken = default)
    {
        List<ContainerInfo> containers = await GetManagedContainersAsync(cancellationToken);
        foreach (ContainerInfo container in containers)
        {
            await DeleteContainerAsync(container.ContainerId, cancellationToken);
        }
        logger?.LogInformation("Deleted {Count} managed containers", containers.Count);
    }

    public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        if (shellPrefix == null || shellPrefix.Length == 0)
        {
            throw new ArgumentException("shellPrefix is required", nameof(shellPrefix));
        }

        string[] full = [.. shellPrefix, command];
        return ExecuteCommandAsync(containerId, full, workingDirectory, timeoutSeconds, cancellationToken);
    }

    public async Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        ContainerExecCreateResponse execCreate = await _client.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = workingDirectory,
            Cmd = command
        }, cancellationToken);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using MultiplexedStream stream = await _client.Exec.StartAndAttachContainerExecAsync(execCreate.ID, tty: false, cts.Token);
        (string? stdout, string? stderr) = await ReadOutputAsync(stream, cts.Token);

        ContainerExecInspectResponse inspect = await _client.Exec.InspectContainerExecAsync(execCreate.ID, cancellationToken);

        sw.Stop();

        // 应用输出截断（保持与旧 CommandResult 行为一致）
        (string truncatedStdout, bool stdoutTruncated) = CommandOutputTruncation.Truncate(stdout ?? string.Empty, config.OutputOptions);
        (string truncatedStderr, bool stderrTruncated) = CommandOutputTruncation.Truncate(stderr ?? string.Empty, config.OutputOptions);

        return new CommandExitEvent(
            truncatedStdout,
            truncatedStderr,
            inspect.ExitCode,
            sw.ElapsedMilliseconds,
            stdoutTruncated || stderrTruncated);
    }

    public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(
        string containerId,
        string[] shellPrefix,
        string command,
        string workingDirectory,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        if (shellPrefix == null || shellPrefix.Length == 0)
        {
            throw new ArgumentException("shellPrefix is required", nameof(shellPrefix));
        }

        string[] full = [.. shellPrefix, command];
        return ExecuteCommandStreamAsync(containerId, full, workingDirectory, timeoutSeconds, cancellationToken);
    }

    private static string? ExtractContainerIp(NetworkSettings? networkSettings)
    {
        if (networkSettings == null) return null;

        if (!string.IsNullOrWhiteSpace(networkSettings.IPAddress))
        {
            return networkSettings.IPAddress;
        }

        if (networkSettings.Networks != null)
        {
            foreach ((_, EndpointSettings? endpoint) in networkSettings.Networks)
            {
                if (!string.IsNullOrWhiteSpace(endpoint?.IPAddress))
                {
                    return endpoint!.IPAddress;
                }
            }
        }

        return null;
    }

    private static string? ExtractContainerIp(IDictionary<string, EndpointSettings>? networks)
    {
        if (networks == null) return null;

        foreach ((_, EndpointSettings? endpoint) in networks)
        {
            if (!string.IsNullOrWhiteSpace(endpoint?.IPAddress))
            {
                return endpoint!.IPAddress;
            }
        }

        return null;
    }

    private async Task<string[]> DetectShellPrefixAsync(string containerId, CancellationToken cancellationToken)
    {
        // 使用最可靠的 bootstrap：Windows 用 cmd，Linux 用 /bin/sh。
        // 输出格式：逗号分隔的一行，例如："pwsh,-NoProfile,-NonInteractive,-Command" 或 "/bin/bash,-lc"。
        const int timeoutSeconds = 10;

        if (config.IsWindowsContainer)
        {
            string script = "where pwsh >nul 2>nul && (echo pwsh,-NoProfile,-NonInteractive,-Command) || (where powershell >nul 2>nul && (echo powershell,-NoProfile,-NonInteractive,-Command) || (echo cmd,/c))";
            CommandExitEvent result = await ExecuteCommandAsync(containerId, ["cmd", "/c", script], "/", timeoutSeconds, cancellationToken);
            return ParseShellPrefixCsv(result.Stdout, fallback: ["cmd", "/c"]);
        }
        else
        {
            // Linux 容器：优先使用 bash（-lc），其次才尝试 pwsh。
            string script = "if command -v bash >/dev/null 2>&1; then echo \"$(command -v bash),-lc\"; " +
                            "elif command -v pwsh >/dev/null 2>&1; then echo 'pwsh,-NoProfile,-NonInteractive,-Command'; " +
                            "elif command -v sh >/dev/null 2>&1; then echo \"$(command -v sh),-lc\"; " +
                            "else echo '/bin/sh,-lc'; fi";

            CommandExitEvent result = await ExecuteCommandAsync(containerId, ["/bin/sh", "-lc", script], "/", timeoutSeconds, cancellationToken);
            return ParseShellPrefixCsv(result.Stdout, fallback: ["/bin/sh", "-lc"]);
        }
    }

    private static string[] ParseShellPrefixCsv(string? stdout, string[] fallback)
    {
        string firstLine = (stdout ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return fallback;
        }

        string[] parts = firstLine
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? fallback : parts;
    }

    public async IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(
        string containerId,
        string[] command,
        string workingDirectory,
        int timeoutSeconds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // IMPORTANT:
        // - progress 阶段 stdout/stderr 不做 truncate。
        // - exit 事件必须与 ExecuteCommandAsync 返回的 CommandExitEvent 字段语义完全一致（包含 truncate + IsTruncated）。
        CommandStreamSummaryBuilder summaryBuilder = new(config.OutputOptions);

        ContainerExecCreateResponse execCreate = await _client.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = workingDirectory,
            Cmd = command
        }, cancellationToken);

        string execId = execCreate.ID;
        logger?.LogDebug("Created exec instance {ExecId} for container {ContainerId}", execId, containerId[..12]);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        MultiplexedStream? stream = null;
        try
        {
            stream = await _client.Exec.StartAndAttachContainerExecAsync(execId, tty: false, cts.Token);

            byte[] buffer = new byte[4096];

            while (!cts.Token.IsCancellationRequested)
            {
                MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cts.Token);

                if (result.EOF || result.Count == 0)
                {
                    break;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // IMPORTANT: progress阶段不做 truncate，让前端实时看到完整 stdout/stderr。
                if (result.Target == MultiplexedStream.TargetStream.StandardOut)
                {
                    summaryBuilder.AppendStdout(text);
                    yield return new CommandStdoutEvent(text);
                }
                else
                {
                    summaryBuilder.AppendStderr(text);
                    yield return new CommandStderrEvent(text);
                }
            }
        }
        finally
        {
            stream?.Dispose();
        }

        sw.Stop();
        long exitCode = -1;
        try
        {
            ContainerExecInspectResponse inspect = await _client.Exec.InspectContainerExecAsync(execId, CancellationToken.None);
            exitCode = inspect.ExitCode;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to inspect exec {ExecId}", execId);
        }

        yield return summaryBuilder.BuildExit(exitCode, sw.ElapsedMilliseconds);
    }

    public async Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default)
    {
        string relativePath = containerPath.TrimStart('/');
        await using MemoryStream tarStream = new();
        using (IWriter writer = WriterFactory.Open(tarStream, ArchiveType.Tar, new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }))
        {
            await using MemoryStream dataStream = new(content);
            writer.Write(relativePath, dataStream, null);
        }
        tarStream.Seek(0, SeekOrigin.Begin);

        await _client.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new ContainerPathStatParameters { Path = "/" },
            tarStream,
            cancellationToken);

        logger?.LogInformation("Uploaded file to container {ContainerId}: {Path} ({Size} bytes)", containerId[..12], containerPath, content.Length);
    }

    /// <summary>
    /// 解析 Linux ls -la --full-time 命令的输出
    /// </summary>
    internal static List<FileEntry> ParseLinuxLsOutput(string requestedPath, string output)
        => DockerOutputParser.ParseLinuxLsOutput(requestedPath, output);

    /// <summary>
    /// 解析 Windows dir 命令的输出
    /// </summary>
    internal static List<FileEntry> ParseWindowsDirOutput(string requestedPath, string output)
        => DockerOutputParser.ParseWindowsDirOutput(requestedPath, output);

    internal static async Task<List<FileEntry>> ListDirectoryFromTarStreamAsync(string requestedPath, Stream tarStream, CancellationToken cancellationToken)
    {
        List<FileEntry> entries = new();

        await foreach (TarEntryInfo entry in EnumerateTarEntriesAsync(tarStream, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            string? fullPath = TryGetFullPathFromArchiveEntry(requestedPath, entry.Name);
            if (string.IsNullOrEmpty(fullPath))
            {
                continue;
            }

            entries.Add(new FileEntry
            {
                Path = fullPath,
                Name = Path.GetFileName(fullPath.TrimEnd('/')),
                IsDirectory = entry.IsDirectory,
                Size = entry.IsDirectory ? 0 : entry.Size,
                LastModified = entry.LastModified
            });
        }

        return entries;
    }

    internal static string? TryGetFullPathFromArchiveEntry(string requestedPath, string entryKey)
    {
        if (string.IsNullOrWhiteSpace(requestedPath) || string.IsNullOrWhiteSpace(entryKey))
        {
            return null;
        }

        // Docker tar entry keys are typically relative, and often include the directory name once.
        // Example: requested "/app/artifacts" may yield entry keys like "artifacts/" and "artifacts/hello.txt".
        string normalizedRequested = DockerOutputParser.NormalizeContainerPath(requestedPath);
        string normalizedRequestedNoTrailing = normalizedRequested.TrimEnd('/');

        string cleanKey = entryKey.Replace('\\', '/').TrimStart('.', '/');
        cleanKey = cleanKey.Trim();
        if (string.IsNullOrEmpty(cleanKey))
        {
            return null;
        }

        // 1) Strip the full requested path (relative form) if the archive key includes it.
        string requestedRelative = normalizedRequestedNoTrailing.TrimStart('/');
        if (!string.IsNullOrEmpty(requestedRelative))
        {
            cleanKey = StripPrefix(cleanKey, requestedRelative);
        }

        // 2) Also strip a single leading directory matching the requested basename (common Docker behavior).
        string requestedBaseName = GetContainerBaseName(normalizedRequestedNoTrailing);
        if (!string.IsNullOrEmpty(requestedBaseName))
        {
            cleanKey = StripPrefix(cleanKey, requestedBaseName);
        }

        cleanKey = cleanKey.TrimStart('/').TrimEnd('/');
        if (string.IsNullOrEmpty(cleanKey))
        {
            // This is the root directory entry itself.
            return null;
        }

        string combined = DockerOutputParser.CombineContainerPath(normalizedRequestedNoTrailing, cleanKey);
        if (string.Equals(combined.TrimEnd('/'), normalizedRequestedNoTrailing.TrimEnd('/'), StringComparison.Ordinal))
        {
            return null;
        }

        return combined;
    }

    private static string StripPrefix(string key, string prefix)
    {
        if (string.Equals(key, prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string withSlash = prefix.EndsWith('/') ? prefix : prefix + "/";
        return key.StartsWith(withSlash, StringComparison.Ordinal) ? key[withSlash.Length..] : key;
    }

    private static string GetContainerBaseName(string pathNoTrailing)
    {
        string p = pathNoTrailing.TrimEnd('/');
        if (p == "/" || string.IsNullOrEmpty(p))
        {
            return string.Empty;
        }

        int idx = p.LastIndexOf('/');
        return idx >= 0 ? p[(idx + 1)..] : p;
    }

    public async Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
    {
        GetArchiveFromContainerResponse archive = await _client.Containers.GetArchiveFromContainerAsync(
            containerId,
            new GetArchiveFromContainerParameters { Path = filePath },
            false,
            cancellationToken);

        await using Stream stream = archive.Stream;
        byte[]? bytes = await ExtractFirstFileBytesFromTarStreamAsync(stream, cancellationToken);
        return bytes ?? throw new FileNotFoundException($"File {filePath} not found in container");
    }

    internal static async Task<byte[]?> ExtractFirstFileBytesFromTarStreamAsync(Stream tarStream, CancellationToken cancellationToken)
    {
        await foreach (TarEntryInfo entry in EnumerateTarEntriesAsync(tarStream, cancellationToken))
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            if (entry.Data == null)
            {
                continue;
            }

            return entry.Data;
        }

        return null;
    }

    private sealed record TarEntryInfo(string Name, bool IsDirectory, long Size, DateTimeOffset? LastModified, byte[]? Data);

    private static async IAsyncEnumerable<TarEntryInfo> EnumerateTarEntriesAsync(
        Stream tarStream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Why custom tar reader?
        // - Docker 的 archive API 在中文文件名场景会插入 PAX headers（typeflag 'x' / 'g'）。
        // - System.Formats.Tar 在某些 Docker 流（chunked/无完整尾部 padding/终止块）上会必现 EndOfStreamException。
        // - 这里实现一个“更宽容”的顺序读取器：能解析 PAX(path=...)，并对末尾 padding/终止块缺失容错。

        byte[] header = new byte[512];
        string? pendingPaxPath = null;
        string? pendingLongName = null;

        while (true)
        {
            int readHeader = await ReadAtMostAsync(tarStream, header, 0, header.Length, cancellationToken);
            if (readHeader == 0)
            {
                yield break;
            }

            // 容错：如果最后不足 512 字节，视为结束（避免“读过头”）。
            if (readHeader < 512)
            {
                yield break;
            }

            if (IsAllZeroBlock(header))
            {
                // tar 规范：连续两个 512 的全 0 block 表示结束。
                // 这里读第二个 block，如果缺失也容错结束。
                int read2 = await ReadAtMostAsync(tarStream, header, 0, header.Length, cancellationToken);
                yield break;
            }

            TarHeaderInfo h = ParseTarHeader(header);

            // 处理 GNU long name（typeflag 'L'）：其 data 是下一条目的完整文件名。
            if (h.TypeFlag == 'L')
            {
                byte[] data = await ReadDataBestEffortAsync(tarStream, h.Size, cancellationToken);
                pendingLongName = DecodeNullTerminatedString(data);
                await SkipPaddingBestEffortAsync(tarStream, h.Size, cancellationToken);
                continue;
            }

            // 处理 PAX（typeflag 'x' / 'g'）：其 data 是 key=value 记录，最重要的是 path。
            if (h.TypeFlag is 'x' or 'g')
            {
                byte[] data = await ReadDataBestEffortAsync(tarStream, h.Size, cancellationToken);
                pendingPaxPath = TryParsePaxPath(data);
                await SkipPaddingBestEffortAsync(tarStream, h.Size, cancellationToken);
                continue;
            }

            string effectiveName = pendingPaxPath
                ?? pendingLongName
                ?? h.Name;

            pendingPaxPath = null;
            pendingLongName = null;

            bool isDir = h.TypeFlag == '5' || effectiveName.EndsWith("/", StringComparison.Ordinal);

            if (isDir)
            {
                // 目录通常 size=0；即使有 size，也跳过。
                if (h.Size > 0)
                {
                    await SkipBytesBestEffortAsync(tarStream, h.Size, cancellationToken);
                    await SkipPaddingBestEffortAsync(tarStream, h.Size, cancellationToken);
                }

                yield return new TarEntryInfo(effectiveName, IsDirectory: true, Size: 0, h.LastModified, Data: null);
                continue;
            }

            // 普通文件：读取数据并 yield。
            byte[] fileData = await ReadDataBestEffortAsync(tarStream, h.Size, cancellationToken);
            await SkipPaddingBestEffortAsync(tarStream, h.Size, cancellationToken);
            yield return new TarEntryInfo(effectiveName, IsDirectory: false, Size: h.Size, h.LastModified, fileData);
        }
    }

    private readonly record struct TarHeaderInfo(string Name, long Size, char TypeFlag, DateTimeOffset? LastModified);

    private static TarHeaderInfo ParseTarHeader(byte[] header)
    {
        // ustar: name(0-99), size(124-135), mtime(136-147), typeflag(156), prefix(345-499)
        string name = DecodeTarString(header, 0, 100);
        string prefix = DecodeTarString(header, 345, 155);
        string fullName = string.IsNullOrEmpty(prefix) ? name : (prefix.TrimEnd('/') + "/" + name.TrimStart('/'));

        long size = ParseTarNumber(header, 124, 12);
        long mtimeSeconds = ParseTarNumber(header, 136, 12);
        DateTimeOffset? mtime = mtimeSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(mtimeSeconds)
            : null;

        char typeFlag = header[156] == 0 ? '0' : (char)header[156];
        return new TarHeaderInfo(fullName, size, typeFlag, mtime);
    }

    private static string DecodeTarString(byte[] buffer, int offset, int length)
    {
        int end = offset;
        int max = offset + length;
        while (end < max && buffer[end] != 0)
        {
            end++;
        }

        // tar 字段可能包含尾部空格
        return Encoding.UTF8.GetString(buffer, offset, end - offset).Trim();
    }

    private static long ParseTarNumber(byte[] buffer, int offset, int length)
    {
        // 兼容两种编码：
        // 1) ASCII 八进制（最常见）
        // 2) base-256（二进制，首字节最高位为 1）
        if (length <= 0) return 0;

        byte first = buffer[offset];
        if ((first & 0x80) != 0)
        {
            // base-256，二进制补码
            long value = 0;
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }

            // 清除最高位
            value &= ~(1L << (length * 8 - 1));
            return value;
        }

        long result = 0;
        int end = offset + length;
        for (int i = offset; i < end; i++)
        {
            byte b = buffer[i];
            if (b == 0 || b == (byte)' ') break;
            if (b < '0' || b > '7') continue;
            result = (result << 3) + (b - (byte)'0');
        }

        return result;
    }

    private static bool IsAllZeroBlock(byte[] block)
    {
        for (int i = 0; i < block.Length; i++)
        {
            if (block[i] != 0) return false;
        }

        return true;
    }

    private static string DecodeNullTerminatedString(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        if (end < 0) end = bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, end).Trim();
    }

    private static string? TryParsePaxPath(byte[] paxBytes)
    {
        // PAX record format (byte-based): "<len> <key>=<value>\n" repeated.
        // NOTE: <len> is the total RECORD LENGTH IN BYTES (including digits, space and trailing '\n').
        int idx = 0;
        while (idx < paxBytes.Length)
        {
            int space = Array.IndexOf(paxBytes, (byte)' ', idx);
            if (space < 0)
            {
                break;
            }

            // parse length digits (ASCII)
            int recordLen = 0;
            for (int i = idx; i < space; i++)
            {
                byte b = paxBytes[i];
                if (b < '0' || b > '9')
                {
                    recordLen = 0;
                    break;
                }
                recordLen = recordLen * 10 + (b - (byte)'0');
            }

            if (recordLen <= 0)
            {
                break;
            }

            int recordStart = space + 1;
            int recordEnd = idx + recordLen;
            if (recordEnd > paxBytes.Length)
            {
                recordEnd = paxBytes.Length;
            }

            int recordDataLen = Math.Max(0, recordEnd - recordStart);
            if (recordDataLen == 0)
            {
                idx = recordEnd;
                continue;
            }

            // Drop trailing '\n' if present.
            int trimmedEnd = recordEnd;
            if (trimmedEnd > recordStart && paxBytes[trimmedEnd - 1] == (byte)'\n')
            {
                trimmedEnd--;
            }

            ReadOnlySpan<byte> record = paxBytes.AsSpan(recordStart, Math.Max(0, trimmedEnd - recordStart));
            int eq = record.IndexOf((byte)'=');
            if (eq > 0)
            {
                string key = Encoding.UTF8.GetString(record.Slice(0, eq));
                if (string.Equals(key, "path", StringComparison.Ordinal))
                {
                    string value = Encoding.UTF8.GetString(record.Slice(eq + 1));
                    return value.Trim();
                }
            }

            idx = recordEnd;
        }

        return null;
    }

    private static async Task<byte[]> ReadDataBestEffortAsync(Stream stream, long size, CancellationToken cancellationToken)
    {
        if (size <= 0) return Array.Empty<byte>();
        if (size > int.MaxValue) throw new NotSupportedException($"Tar entry too large: {size}");

        byte[] buffer = new byte[(int)size];
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                // 容错：流提前结束时返回已读部分（避免抛 EndOfStreamException）。
                if (offset == 0) return Array.Empty<byte>();
                Array.Resize(ref buffer, offset);
                return buffer;
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task SkipPaddingBestEffortAsync(Stream stream, long size, CancellationToken cancellationToken)
    {
        long pad = (512 - (size % 512)) % 512;
        if (pad <= 0) return;
        await SkipBytesBestEffortAsync(stream, pad, cancellationToken);
    }

    private static async Task SkipBytesBestEffortAsync(Stream stream, long count, CancellationToken cancellationToken)
    {
        if (count <= 0) return;
        byte[] buf = new byte[8192];
        long remaining = count;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buf.Length, remaining);
            int read = await stream.ReadAsync(buf.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                return;
            }

            remaining -= read;
        }
    }

    private static async Task<int> ReadAtMostAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + total, count - total), cancellationToken);
            if (read == 0)
            {
                break;
            }
            total += read;
        }
        return total;
    }

    public async Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ContainerStatsResponse? statsData = null;

        // 使用同步 Action 来捕获数据
        Progress<ContainerStatsResponse> progress = new(stats =>
        {
            statsData = stats;
        });

        // 使用新的 API 签名获取容器统计信息（一次性读取）
        await _client.Containers.GetContainerStatsAsync(
            containerId,
            new ContainerStatsParameters { Stream = false },
            progress,
            cancellationToken);

        // 等待一小段时间确保回调已执行
        await Task.Delay(50, cancellationToken);

        if (statsData == null)
        {
            logger?.LogWarning("No stats data received for container {ContainerId}", containerId[..12]);
            return null;
        }

        SessionUsage usage = new()
        {
            ContainerId = containerId
        };

        // CPU 使用
        if (statsData.CPUStats?.CPUUsage != null)
        {
            usage.CpuUsageNanos = (long)statsData.CPUStats.CPUUsage.TotalUsage;
        }

        // 内存使用
        if (statsData.MemoryStats != null)
        {
            usage.MemoryUsageBytes = (long)statsData.MemoryStats.Usage;
            usage.PeakMemoryBytes = (long)statsData.MemoryStats.MaxUsage;
        }

        // 网络 IO
        if (statsData.Networks != null)
        {
            foreach (NetworkStats? network in statsData.Networks.Values)
            {
                usage.NetworkRxBytes += (long)network.RxBytes;
                usage.NetworkTxBytes += (long)network.TxBytes;
            }
        }

        return usage;
    }

    private static async Task<(string stdout, string stderr)> ReadOutputAsync(MultiplexedStream stream, CancellationToken cancellationToken)
    {
        StringBuilder stdout = new();
        StringBuilder stderr = new();
        byte[] buffer = new byte[8192];

        while (true)
        {
            MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF || result.Count == 0)
                break;

            string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (result.Target == MultiplexedStream.TargetStream.StandardOut)
            {
                stdout.Append(text);
            }
            else
            {
                stderr.Append(text);
            }
        }

        return (stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
