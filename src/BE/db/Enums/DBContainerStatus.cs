namespace Chats.DB.Enums;

/// <summary>
/// Lifecycle state for a container resource. Values map to ContainerResource.Status.
/// </summary>
public enum DBContainerStatus : byte
{
    Running = 1,
    Stopped = 2,
    Pending = 3,
    Deleted = 4,
}
