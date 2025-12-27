namespace Chats.DockerInterface.Models;

/// <summary>
/// 命令输出流事件类型
/// </summary>
public enum CommandOutputType
{
    /// <summary>
    /// 标准输出
    /// </summary>
    Stdout,

    /// <summary>
    /// 标准错误
    /// </summary>
    Stderr,

    /// <summary>
    /// 命令完成
    /// </summary>
    Exit
}
