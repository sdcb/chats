using Chats.DockerInterface.Models;

namespace Chats.DockerInterface;

/// <summary>
/// Docker服务接口
/// </summary>
public interface IDockerService : IDisposable
{
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
    /// 执行 shell 命令
    /// </summary>
    Task<CommandResult> ExecuteCommandAsync(string containerId, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行命令数组（直接执行，不经过 shell 包装）
    /// </summary>
    Task<CommandResult> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式执行 shell 命令
    /// </summary>
    IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式执行命令数组（直接执行，不经过 shell 包装）
    /// </summary>
    IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传文件到容器
    /// </summary>
    Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出容器中的目录
    /// </summary>
    Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从容器下载文件
    /// </summary>
    Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取容器使用量统计
    /// </summary>
    Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);
}
