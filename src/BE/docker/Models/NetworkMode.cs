namespace Chats.DockerInterface.Models;

/// <summary>
/// 网络模式
/// </summary>
public enum NetworkMode
{
    /// <summary>
    /// 完全禁用网络（最安全，推荐用于代码执行）
    /// </summary>
    None,

    /// <summary>
    /// 桥接网络（可访问外网）
    /// </summary>
    Bridge,

    /// <summary>
    /// 主机网络（共享主机网络栈，不推荐）
    /// </summary>
    Host
}
