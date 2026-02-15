using Chats.DockerInterface.Models;

namespace Chats.DockerInterface;

/// <summary>
/// CodePod SDK 配置
/// </summary>
public class CodePodConfig
{
    /// <summary>
    /// 是否使用 Windows 容器（默认 false，使用 Linux 容器）
    /// </summary>
    public bool IsWindowsContainer { get; set; }

    /// <summary>
    /// Docker 服务端点地址。
    /// 如果为 null，将根据操作系统自动选择默认地址：
    /// - Windows: npipe://./pipe/docker_engine
    /// - Linux/macOS: unix:///var/run/docker.sock
    /// </summary>
    public string? DockerEndpoint { get; set; } = null;

    /// <summary>
    /// 获取实际的 Docker 端点 URI
    /// </summary>
    public Uri GetDockerEndpointUri()
    {
        // 优先使用配置中的端点
        if (!string.IsNullOrWhiteSpace(DockerEndpoint))
        {
            return new Uri(DockerEndpoint);
        }

        // 不依赖当前宿主机操作系统；仅基于配置的容器平台做默认选择
        return IsWindowsContainer
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
    }

    /// <summary>
    /// 获取容器保持运行的命令
    /// Linux: tail -f /dev/null
    /// Windows: cmd /c "ping -t localhost" (使用原生命令，启动更快)
    /// </summary>
    public string[] GetKeepAliveCommand()
    {
        return IsWindowsContainer
            ? ["cmd", "/c", "ping -t localhost"]
            : ["/bin/sh", "-lc", "tail -f /dev/null"];
    }

    /// <summary>
    /// 获取创建目录的命令
    /// Linux: mkdir -p
    /// Windows: cmd /c mkdir (使用原生命令，启动更快)
    /// </summary>
    public string GetMkdirCommand(params string[] paths)
    {
        if (IsWindowsContainer)
        {
            // Windows cmd: 使用 mkdir 创建目录 (如果存在则忽略)
            IEnumerable<string> commands = paths.Select(p => $"if not exist \"{p.Replace('/', '\\')}\" mkdir \"{p.Replace('/', '\\')}\"");
            return string.Join(" & ", commands);
        }
        else
        {
            return $"mkdir -p {string.Join(" ", paths)}";
        }
    }

    /// <summary>
    /// 获取删除文件的命令
    /// Linux: rm -f "path"
    /// Windows (pwsh): Remove-Item -Force "path"
    /// </summary>
    public string GetDeleteFileCommand(string filePath)
    {
        if (IsWindowsContainer)
        {
            return $"Remove-Item -Force -ErrorAction SilentlyContinue '{filePath}'";
        }
        else
        {
            return $"rm -f \"{filePath}\"";
        }
    }

    /// <summary>
    /// 容器工作目录
    /// </summary>
    public string WorkDir { get; set; } = "/app";

    /// <summary>
    /// 容器标签前缀，用于标识由本系统管理的容器
    /// </summary>
    public string LabelPrefix { get; set; } = "codepod";

    /// <summary>
    /// 默认资源限制（用于创建容器时的最大上限）
    /// </summary>
    public ResourceLimits MaxResourceLimits { get; set; } = new()
    {
        // 0 means unlimited (validated by ResourceLimits.Validate)
        MemoryBytes = 0,
        CpuCores = 0,
        MaxProcesses = 0,
    };

    /// <summary>
    /// 默认资源限制（用于不指定限制时的默认值）
    /// </summary>
    public ResourceLimits DefaultResourceLimits { get; set; } = ResourceLimits.Standard;

    /// <summary>
    /// 默认网络模式。推荐使用 None 以获得最佳安全性
    /// </summary>
    public NetworkMode DefaultNetworkMode { get; set; } = NetworkMode.None;

    /// <summary>
    /// 输出配置
    /// </summary>
    public OutputOptions OutputOptions { get; set; } = new();

    /// <summary>
    /// Artifacts 目录路径（相对于 WorkDir）
    /// </summary>
    public string ArtifactsDir { get; set; } = "artifacts";

    /// <summary>
    /// 获取列出目录的命令
    /// Linux: ls -la --full-time "path" (输出详细信息包括类型、大小、修改时间)
    /// Windows: cmd /c dir /a /t:w "path" (使用原生命令)
    /// </summary>
    public string[] GetListDirectoryCommand(string path)
    {
        if (IsWindowsContainer)
        {
            // Windows cmd: dir /a 显示所有文件包括隐藏文件, /t:w 显示最后写入时间
            return ["cmd", "/c", $"dir /a /t:w \"{path.Replace('/', '\\')}\""];
        }
        else
        {
            // Linux ls: -l 长格式, -a 包括隐藏文件, --full-time 完整时间格式
            return ["ls", "-la", "--full-time", path];
        }
    }
}

