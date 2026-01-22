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
