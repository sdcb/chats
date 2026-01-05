namespace Chats.DockerInterface.Models;

/// <summary>
/// 容器信息
/// </summary>
public class ContainerInfo
{
    /// <summary>
    /// 容器ID
    /// </summary>
    public required string ContainerId { get; init; }

    /// <summary>
    /// 容器短ID（前12位）
    /// </summary>
    public string ShortId => ContainerId.Length >= 12 ? ContainerId[..12] : ContainerId;

    /// <summary>
    /// 容器名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 镜像名称
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// Docker原生状态
    /// </summary>
    public required string DockerStatus { get; init; }

    /// <summary>
    /// 容器状态
    /// </summary>
    public ContainerStatus Status { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// 容器标签
    /// </summary>
    public Dictionary<string, string> Labels { get; init; } = new();

    /// <summary>
    /// Shell 前缀（不包含实际命令参数），用于拼接执行命令。
    /// 例如：Linux: ["/bin/bash", "-lc"], Windows: ["pwsh", "-NoProfile", "-NonInteractive", "-Command"]
    /// </summary>
    public required string[] ShellPrefix { get; init; }

    /// <summary>
    /// 容器 IP（可能为空，例如 host/none 网络模式）
    /// </summary>
    public string? Ip { get; init; }
}
