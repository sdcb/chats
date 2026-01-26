namespace Chats.DockerInterface.Exceptions;

/// <summary>
/// 容器不存在异常
/// </summary>
public class ContainerNotFoundException : Exception
{
    public string ContainerId { get; }

    public ContainerNotFoundException(string containerId)
        : base($"Container {containerId} not found or has been deleted")
    {
        ContainerId = containerId;
    }

    public ContainerNotFoundException(string containerId, Exception innerException)
        : base($"Container {containerId} not found or has been deleted", innerException)
    {
        ContainerId = containerId;
    }
}

/// <summary>
/// 容器内路径不存在异常
/// </summary>
public class ContainerPathNotFoundException : Exception
{
    public string ContainerId { get; }
    public string Path { get; }

    public ContainerPathNotFoundException(string containerId, string path)
        : base($"Path '{path}' not found in container {containerId}")
    {
        ContainerId = containerId;
        Path = path;
    }

    public ContainerPathNotFoundException(string containerId, string path, Exception innerException)
        : base($"Path '{path}' not found in container {containerId}", innerException)
    {
        ContainerId = containerId;
        Path = path;
    }
}
