namespace Chats.DockerInterface.Models;

/// <summary>
/// 容器状态枚举
/// </summary>
public enum ContainerStatus
{
    /// <summary>
    /// 预热中 - 容器正在创建和启动
    /// </summary>
    Warming,

    /// <summary>
    /// 空闲 - 容器已预热完成，等待分配
    /// </summary>
    Idle,

    /// <summary>
    /// 繁忙 - 容器已分配给会话
    /// </summary>
    Busy,

    /// <summary>
    /// 销毁中 - 容器正在被删除
    /// </summary>
    Destroying
}
