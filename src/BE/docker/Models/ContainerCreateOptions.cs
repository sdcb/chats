namespace Chats.DockerInterface.Models;

/// <summary>
/// Extra backend options applied when creating a container: named-volume mounts,
/// published port bindings and additional labels.
/// </summary>
public sealed record ContainerCreateOptions
{
    public IReadOnlyList<ContainerVolumeMountSpec> Mounts { get; init; } = [];

    public IReadOnlyList<ContainerPortBindingSpec> PortBindings { get; init; } = [];

    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}

/// <param name="Source">Backend volume name (docker named volume).</param>
/// <param name="Target">Absolute path inside the container.</param>
public sealed record ContainerVolumeMountSpec(string Source, string Target, bool ReadOnly);

/// <param name="Protocol">"tcp" or "udp".</param>
public sealed record ContainerPortBindingSpec(int ContainerPort, int HostPort, string Protocol = "tcp", string HostIp = "0.0.0.0");
