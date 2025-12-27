namespace Chats.DockerInterface.Models;

/// <summary>
/// 网络模式扩展方法
/// </summary>
public static class NetworkModeExtensions
{
    /// <summary>
    /// 转换为 Docker NetworkMode 字符串
    /// Linux 容器支持: none, bridge, host
    /// Windows 容器支持: nat (默认), transparent, l2bridge, overlay 等
    /// Windows 容器不支持 "none" 网络模式
    /// </summary>
    public static string ToDockerNetworkMode(this NetworkMode mode, bool isWindowsContainer = false)
    {
        if (isWindowsContainer)
        {
            // Windows 容器不支持 "none" 网络，使用 "nat" 作为默认
            return mode switch
            {
                NetworkMode.None => "nat",      // Windows 不支持 none，降级到 nat
                NetworkMode.Bridge => "nat",    // Windows 使用 nat 而不是 bridge
                NetworkMode.Host => "nat",      // Windows 通常也不支持完整的 host 模式
                _ => "nat"
            };
        }

        // Linux 容器
        return mode switch
        {
            NetworkMode.None => "none",
            NetworkMode.Bridge => "bridge",
            NetworkMode.Host => "host",
            _ => "none"
        };
    }
}
