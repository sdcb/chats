namespace Chats.DockerInterface.Models;

/// <summary>
/// 命令流式输出事件
/// </summary>
public class CommandOutputEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public CommandOutputType Type { get; init; }

    /// <summary>
    /// 输出数据（当 Type 为 Stdout 或 Stderr 时）
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// 退出码（当 Type 为 Exit 时）
    /// </summary>
    public long? ExitCode { get; init; }

    /// <summary>
    /// 执行时间（当 Type 为 Exit 时）
    /// </summary>
    public long? ExecutionTimeMs { get; init; }

    public static CommandOutputEvent FromStdout(string data) =>
        new() { Type = CommandOutputType.Stdout, Data = data };

    public static CommandOutputEvent FromStderr(string data) =>
        new() { Type = CommandOutputType.Stderr, Data = data };

    public static CommandOutputEvent FromExit(long exitCode, long executionTimeMs) =>
        new() { Type = CommandOutputType.Exit, ExitCode = exitCode, ExecutionTimeMs = executionTimeMs };
}
