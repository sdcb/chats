namespace Chats.DB.Enums;

/// <summary>
/// Container runtime backend type. Values map to ContainerRuntimeNode.BackendType.
/// </summary>
public enum DBContainerBackendType : byte
{
    Docker = 1,
    WindowsDocker = 2,
    Kubernetes = 3,
    Other = 4,
}
