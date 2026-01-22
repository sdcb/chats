namespace Chats.DockerInterface.Models;

/// <summary>
/// 输出截断策略
/// </summary>
public enum TruncationStrategy
{
    /// <summary>
    /// 保留开头部分
    /// </summary>
    Head,

    /// <summary>
    /// 保留结尾部分
    /// </summary>
    Tail,

    /// <summary>
    /// 保留首尾，中间省略
    /// </summary>
    HeadAndTail
}
