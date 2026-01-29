using Chats.DockerInterface;
using Chats.DockerInterface.Models;

namespace Chats.BE.UnitTest.CodeInterpreter;

/// <summary>
/// 可配置的 FakeDockerService，用于单元测试。
/// 通过设置属性和委托来自定义行为。
/// </summary>
public sealed class FakeDockerService : IDockerService
{
    public record UploadCall(string ContainerId, string Path, byte[] Content);

    private readonly Dictionary<(string containerId, string filePath), byte[]> _files = new();

    /// <summary>
    /// 配置对象
    /// </summary>
    public CodePodConfig Config { get; set; } = new();

    #region 调用跟踪

    /// <summary>
    /// CreateContainerAsync 被调用的次数
    /// </summary>
    public int CreateContainerCalls { get; private set; }

    /// <summary>
    /// EnsureImageAsync 是否被调用
    /// </summary>
    public bool EnsureImageCalled { get; private set; }

    /// <summary>
    /// CreateContainerAsync 是否被调用
    /// </summary>
    public bool CreateContainerCalled { get; private set; }

    /// <summary>
    /// 所有上传调用的列表
    /// </summary>
    public List<UploadCall> Uploads { get; } = [];

    /// <summary>
    /// 最后一次上传的内容
    /// </summary>
    public byte[]? LastUploadedContent { get; private set; }

    /// <summary>
    /// 最后一次上传的容器 ID
    /// </summary>
    public string? LastUploadedContainerId { get; private set; }

    /// <summary>
    /// 最后一次上传的路径
    /// </summary>
    public string? LastUploadedPath { get; private set; }

    /// <summary>
    /// 最后一次下载的容器 ID
    /// </summary>
    public string? LastDownloadContainerId { get; private set; }

    /// <summary>
    /// 最后一次下载的路径
    /// </summary>
    public string? LastDownloadPath { get; private set; }

    #endregion

    #region 行为配置

    /// <summary>
    /// 设置为 true 时，EnsureImageAsync 和 CreateContainerAsync 会抛出异常（用于验证不应该被调用的场景）
    /// </summary>
    public bool ThrowOnDockerCalls { get; set; }

    /// <summary>
    /// 是否规范化路径（相对路径转换为绝对路径）
    /// </summary>
    public bool NormalizePaths { get; set; }

    /// <summary>
    /// 自定义 CreateContainerAsync 返回的容器信息
    /// </summary>
    public Func<string, ResourceLimits?, NetworkMode?, int, ContainerInfo>? CreateContainerHandler { get; set; }

    #endregion

    #region 文件操作

    /// <summary>
    /// 添加一个模拟文件到 fake 存储中
    /// </summary>
    public void AddFile(string containerId, string filePath, byte[] content)
    {
        string normalized = NormalizePaths ? NormalizePath(filePath) : filePath;
        _files[(containerId, normalized)] = content;
    }

    private string NormalizePath(string path)
    {
        if (path.StartsWith('/'))
        {
            return path;
        }
        string workDir = Config.WorkDir;
        return $"{workDir}/{path}";
    }

    #endregion

    #region IDockerService 实现

    public void Dispose() { }

    public Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
    {
        EnsureImageCalled = true;
        if (ThrowOnDockerCalls)
        {
            throw new InvalidOperationException("Docker should not be called in this test");
        }
        return Task.CompletedTask;
    }

    public Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
    {
        CreateContainerCalled = true;
        CreateContainerCalls++;

        if (ThrowOnDockerCalls)
        {
            throw new InvalidOperationException("Docker should not be called in this test");
        }

        if (CreateContainerHandler != null)
        {
            return Task.FromResult(CreateContainerHandler(image, resourceLimits, networkMode, CreateContainerCalls));
        }

        return Task.FromResult(new ContainerInfo
        {
            ContainerId = $"container-{CreateContainerCalls:D2}-abcdef0123456789",
            Name = $"codepod-test-{CreateContainerCalls:D2}",
            Image = image,
            DockerStatus = "running",
            CreatedAt = DateTimeOffset.UtcNow,
            ShellPrefix = ["/bin/sh", "-lc"],
        });
    }

    public Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ContainerInfo>());

    public Task<List<string>> ListImagesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<string>());

    public Task<ContainerInfo?> GetContainerAsync(string containerId, CancellationToken cancellationToken = default)
        => Task.FromResult<ContainerInfo?>(null);

    public Task DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAllManagedContainersAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default)
    {
        LastUploadedContainerId = containerId;
        LastUploadedPath = containerPath;
        LastUploadedContent = content;
        Uploads.Add(new UploadCall(containerId, containerPath, content));
        return Task.CompletedTask;
    }

    public Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<FileEntry>());

    public Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizePaths ? NormalizePath(filePath) : filePath;
        LastDownloadContainerId = containerId;
        LastDownloadPath = normalized;

        if (_files.TryGetValue((containerId, normalized), out byte[]? content))
        {
            return Task.FromResult(content);
        }

        throw new InvalidOperationException($"File not found in fake docker: {containerId}:{normalized}");
    }

    public Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
        => Task.FromResult<SessionUsage?>(null);

    #endregion
}
