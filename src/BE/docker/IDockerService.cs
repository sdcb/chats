using Chats.DockerInterface.Exceptions;
using Chats.DockerInterface.Models;

namespace Chats.DockerInterface;

/// <summary>
/// Docker服务接口
/// </summary>
public interface IDockerService : IDisposable
{
    /// <summary>
    /// 获取配置（用于接口默认实现）
    /// </summary>
    CodePodConfig Config { get; }
    /// <summary>
    /// 确保镜像存在
    /// </summary>
    /// <param name="image">Docker镜像名称</param>
    Task EnsureImageAsync(string image, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建并启动容器（resourceLimits/networkMode 为 null 时使用默认值）
    /// </summary>
    /// <param name="image">Docker镜像名称</param>
    Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有受管理的容器
    /// </summary>
    Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出本机 Docker daemon 的镜像标签（RepoTags）。
    /// </summary>
    Task<List<string>> ListImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取容器详情
    /// </summary>
    Task<ContainerInfo?> GetContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除容器
    /// </summary>
    Task DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除所有受管理的容器
    /// </summary>
    Task DeleteAllManagedContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行 shell 命令（使用指定的 shell 前缀进行包装）
    /// </summary>
    Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行命令数组（直接执行，不经过 shell 包装）
    /// </summary>
    Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式执行 shell 命令（使用指定的 shell 前缀进行包装）
    /// </summary>
    IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式执行命令数组（直接执行，不经过 shell 包装）
    /// </summary>
    IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传文件到容器
    /// </summary>
    Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出容器中的目录（默认实现基于 ExecuteCommandAsync）
    /// </summary>
    async Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default)
    {
        string[] command = Config.GetListDirectoryCommand(path);
        CommandExitEvent result = await ExecuteCommandAsync(containerId, command, "/", 30, cancellationToken);

        if (result.ExitCode != 0)
        {
            string errorLower = result.Stderr?.ToLowerInvariant() ?? "";
            if (errorLower.Contains("no such file") || errorLower.Contains("cannot find") ||
                errorLower.Contains("cannot access") || errorLower.Contains("not exist"))
            {
                throw new ContainerPathNotFoundException(containerId, path, new Exception(result.Stderr));
            }
            throw new InvalidOperationException($"Failed to list directory: {result.Stderr}");
        }

        return Config.IsWindowsContainer
            ? DockerOutputParser.ParseWindowsDirOutput(path, result.Stdout ?? "")
            : DockerOutputParser.ParseLinuxLsOutput(path, result.Stdout ?? "");
    }

    /// <summary>
    /// 从容器下载文件
    /// </summary>
    Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取容器使用量统计
    /// </summary>
    Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);
}
