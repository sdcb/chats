using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Chats.DockerInterface.Exceptions;
using Chats.DockerInterface.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SdkContainerStatus = Chats.DockerInterface.Models.ContainerStatus;

namespace Chats.DockerInterface;

/// <summary>
/// Docker服务实现
/// </summary>
public class DockerService(CodePodConfig config, ILogger<DockerService>? logger = null) : IDockerService
{
    private readonly DockerClient _client = new DockerClientConfiguration(config.GetDockerEndpointUri()).CreateClient();

    public async Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
    {
        await WrapDockerOperationAsync("EnsureImage", async () =>
        {
            try
            {
                await _client.Images.InspectImageAsync(image, cancellationToken);
                logger?.LogInformation("Image {Image} already exists", image);
            }
            catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger?.LogInformation("Pulling image {Image}...", image);
                await _client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = image },
                    null,
                    new Progress<JSONMessage>(m =>
                    {
                        if (!string.IsNullOrEmpty(m.Status))
                        {
                            logger?.LogDebug("{Status}", m.Status);
                        }
                    }),
                    cancellationToken);
                logger?.LogInformation("Image {Image} pulled successfully", image);
            }
        });
    }

    public async Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
    {
        return await WrapDockerOperationAsync("CreateContainer", async () =>
        {
            try
            {
                return await CreateContainerCoreAsync(image, resourceLimits, networkMode, cancellationToken);
            }
            catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Image may not exist locally when preloadImages=false. Pull once and retry.
                await EnsureImageAsync(image, cancellationToken);
                return await CreateContainerCoreAsync(image, resourceLimits, networkMode, cancellationToken);
            }
        });
    }

    private async Task<ContainerInfo> CreateContainerCoreAsync(string image, ResourceLimits? resourceLimits, NetworkMode? networkMode, CancellationToken cancellationToken)
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
        return await WrapDockerOperationAsync("GetManagedContainers", async () =>
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
        });
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
        return await WrapDockerOperationAsync("ExecuteCommand", async () =>
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
        }, containerId);
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
        await WrapDockerOperationAsync("UploadFile", async () =>
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
        }, containerId);
    }

    public async Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default)
    {
        return await WrapDockerOperationAsync("ListDirectory", async () =>
        {
            List<FileEntry> entries = new();

            GetArchiveFromContainerResponse archive = await _client.Containers.GetArchiveFromContainerAsync(
                containerId,
                new GetArchiveFromContainerParameters { Path = path },
                false,
                cancellationToken);

            await using Stream stream = archive.Stream;
            using IReader reader = ReaderFactory.Open(stream);

            while (reader.MoveToNextEntry())
            {
                IEntry entry = reader.Entry;
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                string? fullPath = TryGetFullPathFromArchiveEntry(path, entry.Key);
                if (string.IsNullOrEmpty(fullPath))
                    continue;

                entries.Add(new FileEntry
                {
                    Path = fullPath,
                    Name = Path.GetFileName(fullPath.TrimEnd('/')),
                    IsDirectory = entry.IsDirectory,
                    Size = entry.Size,
                    LastModified = entry.LastModifiedTime
                });
            }

            return entries;
        }, containerId);
    }

    internal static string? TryGetFullPathFromArchiveEntry(string requestedPath, string entryKey)
    {
        if (string.IsNullOrWhiteSpace(requestedPath) || string.IsNullOrWhiteSpace(entryKey))
        {
            return null;
        }

        // Docker tar entry keys are typically relative, and often include the directory name once.
        // Example: requested "/app/artifacts" may yield entry keys like "artifacts/" and "artifacts/hello.txt".
        string normalizedRequested = NormalizeContainerPath(requestedPath);
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

        string combined = CombineContainerPath(normalizedRequestedNoTrailing, cleanKey);
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

    private static string NormalizeContainerPath(string path)
    {
        string p = path.Replace('\\', '/').Trim();
        if (string.IsNullOrEmpty(p))
        {
            return "/";
        }

        if (!p.StartsWith('/'))
        {
            p = "/" + p;
        }

        // Avoid "//" except for root.
        while (p.Length > 1 && p.Contains("//", StringComparison.Ordinal))
        {
            p = p.Replace("//", "/", StringComparison.Ordinal);
        }

        return p;
    }

    private static string CombineContainerPath(string basePathNoTrailing, string relativeNoLeading)
    {
        string basePath = NormalizeContainerPath(basePathNoTrailing).TrimEnd('/');
        string rel = relativeNoLeading.Replace('\\', '/').TrimStart('/');

        if (string.IsNullOrEmpty(basePath) || basePath == "/")
        {
            return "/" + rel;
        }

        return basePath + "/" + rel;
    }

    public async Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
    {
        return await WrapDockerOperationAsync("DownloadFile", async () =>
        {
            GetArchiveFromContainerResponse archive = await _client.Containers.GetArchiveFromContainerAsync(
                containerId,
                new GetArchiveFromContainerParameters { Path = filePath },
                false,
                cancellationToken);

            await using Stream stream = archive.Stream;
            using IReader reader = ReaderFactory.Open(stream);

            while (reader.MoveToNextEntry())
            {
                IEntry entry = reader.Entry;
                if (!entry.IsDirectory)
                {
                    await using MemoryStream memory = new();
                    reader.WriteEntryTo(memory);
                    return memory.ToArray();
                }
            }

            throw new FileNotFoundException($"File {filePath} not found in container");
        }, containerId);
    }

    public async Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return await WrapDockerOperationAsync("GetContainerStats", async () =>
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
        }, containerId);
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

    private async Task<T> WrapDockerOperationAsync<T>(string operation, Func<Task<T>> action, string? containerId = null)
    {
        try
        {
            return await action();
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && containerId != null)
        {
            logger?.LogError(ex, "Container {ContainerId} not found", containerId);
            throw new ContainerNotFoundException(containerId, ex);
        }
        catch (DockerApiException ex)
        {
            logger?.LogError(ex, "Docker API operation {Operation} failed: {StatusCode}", operation, ex.StatusCode);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Docker operation {Operation} failed", operation);
            throw;
        }
    }

    private async Task WrapDockerOperationAsync(string operation, Func<Task> action, string? containerId = null)
    {
        await WrapDockerOperationAsync(operation, async () =>
        {
            await action();
            return true;
        }, containerId);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
