namespace Chats.DB.Enums;

/// <summary>
/// Network access policy for a container. Values map to NetworkPolicy columns.
/// </summary>
public enum DBContainerNetworkPolicy : byte
{
    None = 0,
    Egress = 1,
    Public = 2,
}
