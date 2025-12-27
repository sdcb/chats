namespace Chats.DockerInterface.Models;

/// <summary>
/// 命令执行结果（完整结果，用于非流式响应）
/// </summary>
public class CommandResult
{
    /// <summary>
    /// 标准输出
    /// </summary>
    public string Stdout { get; init; } = string.Empty;

    /// <summary>
    /// 标准错误
    /// </summary>
    public string Stderr { get; init; } = string.Empty;

    /// <summary>
    /// 退出码
    /// </summary>
    public long ExitCode { get; init; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// 输出是否被截断
    /// </summary>
    public bool IsTruncated { get; init; }
}
