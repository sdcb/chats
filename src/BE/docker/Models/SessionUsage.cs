namespace Chats.DockerInterface.Models;

/// <summary>
/// 会话使用量统计
/// </summary>
public class SessionUsage
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 容器ID
    /// </summary>
    public required string ContainerId { get; init; }

    /// <summary>
    /// CPU 使用时间（纳秒）
    /// </summary>
    public long CpuUsageNanos { get; set; }

    /// <summary>
    /// 当前内存使用（字节）
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// 峰值内存使用（字节）
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// 网络接收字节数
    /// </summary>
    public long NetworkRxBytes { get; set; }

    /// <summary>
    /// 网络发送字节数
    /// </summary>
    public long NetworkTxBytes { get; set; }

    /// <summary>
    /// 已执行命令数
    /// </summary>
    public int CommandCount { get; set; }

    /// <summary>
    /// 统计时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 会话创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 会话持续时间
    /// </summary>
    public TimeSpan Duration => DateTimeOffset.UtcNow - CreatedAt;
}
