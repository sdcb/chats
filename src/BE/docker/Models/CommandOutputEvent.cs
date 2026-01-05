namespace Chats.DockerInterface.Models;

/// <summary>
/// 命令流式输出事件（使用继承区分 stdout / stderr / exit）
/// </summary>
public abstract record CommandOutputEvent;

public sealed record CommandStdoutEvent(string Data) : CommandOutputEvent;

public sealed record CommandStderrEvent(string Data) : CommandOutputEvent;

/// <summary>
/// 命令完成事件。
/// 字段与旧 CommandResult 完全一致。
/// </summary>
public sealed record CommandExitEvent(
	string Stdout,
	string Stderr,
	long ExitCode,
	long ExecutionTimeMs,
	bool IsTruncated) : CommandOutputEvent
{
	public bool Success => ExitCode == 0;
}
