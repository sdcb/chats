using Chats.DB;
using Chats.DockerInterface;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.Containers;

public sealed class ContainerBackendFactory(IOptions<CodePodConfig> defaults, ILoggerFactory loggerFactory) : IDisposable
{
    private readonly CodePodConfig _defaults = defaults.Value;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly Dictionary<int, CachedClient> _clients = [];
    private readonly Lock _gate = new();

    public IDockerService Get(ContainerRuntimeNode node)
    {
        lock (_gate)
        {
            string? endpoint = NormalizeEndpoint(node.Endpoint);
            if (_clients.TryGetValue(node.Id, out CachedClient? existing))
            {
                if (string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
                    return existing.Client;

                // The admin may change a node from an old Unix socket to the
                // host default (or another endpoint). Do not keep using the
                // client that was constructed for the previous endpoint.
                existing.Client.Dispose();
                _clients.Remove(node.Id);
            }

            CodePodConfig config = new()
            {
                IsWindowsContainer = false,
                DockerEndpoint = endpoint,
                WorkDir = _defaults.WorkDir,
                LabelPrefix = _defaults.LabelPrefix,
                OutputOptions = _defaults.OutputOptions,
                ArtifactsDir = _defaults.ArtifactsDir,
                MaxResourceLimits = _defaults.MaxResourceLimits,
                DefaultResourceLimits = _defaults.DefaultResourceLimits,
            };
            IDockerService client = new DockerService(config, _loggerFactory.CreateLogger<DockerService>());
            _clients[node.Id] = new CachedClient(endpoint, client);
            return client;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (CachedClient cached in _clients.Values) cached.Client.Dispose();
            _clients.Clear();
        }
    }

    private static string? NormalizeEndpoint(string? endpoint)
        => string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();

    private sealed record CachedClient(string? Endpoint, IDockerService Client);
}
