using Chats.DB;
using Chats.DockerInterface;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.Containers;

public sealed class ContainerBackendFactory(IOptions<CodePodConfig> defaults, ILoggerFactory loggerFactory) : IDisposable
{
    private readonly CodePodConfig _defaults = defaults.Value;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly Dictionary<int, IDockerService> _clients = [];
    private readonly Lock _gate = new();

    public IDockerService Get(ContainerRuntimeNode node)
    {
        lock (_gate)
        {
            if (_clients.TryGetValue(node.Id, out IDockerService? existing)) return existing;
            CodePodConfig config = new()
            {
                IsWindowsContainer = false,
                DockerEndpoint = node.Endpoint,
                WorkDir = _defaults.WorkDir,
                LabelPrefix = _defaults.LabelPrefix,
                OutputOptions = _defaults.OutputOptions,
                ArtifactsDir = _defaults.ArtifactsDir,
                MaxResourceLimits = _defaults.MaxResourceLimits,
                DefaultResourceLimits = _defaults.DefaultResourceLimits,
            };
            IDockerService client = new DockerService(config, _loggerFactory.CreateLogger<DockerService>());
            _clients[node.Id] = client;
            return client;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (IDockerService client in _clients.Values) client.Dispose();
            _clients.Clear();
        }
    }
}
