namespace Chats.DB.Enums;

/// <summary>
/// Network access policy for a container.
/// <para>
/// Persisted as a lower-case string ('none', 'egress', 'public') in the
/// NetworkPolicy columns of ContainerResource and ContainerResourceTemplate, and
/// in the MinNetworkPolicy / MaxNetworkPolicy columns of ContainerRuntimeNode.
/// </para>
/// <para>
/// The underlying numeric values define the permissiveness order
/// (None &lt; Egress &lt; Public) that quota checks rely on. This ordering only
/// exists in C#: the stored strings sort as 'egress' &lt; 'none' &lt; 'public',
/// which is a different - and more permissive - order. Never translate a quota
/// comparison into SQL; load the value and compare the enum in memory.
/// </para>
/// </summary>
public enum DBContainerNetworkPolicy : byte
{
    None = 0,
    Egress = 1,
    Public = 2,
}
