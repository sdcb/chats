using System.Text.Json.Nodes;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Chats.DB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class NetworkModeRestrictionsTests
{
    [Fact]
    public void OptionsValidator_ShouldFail_WhenDefaultExceedsMaxAllowed()
    {
        CodeInterpreterOptions options = new()
        {
            DefaultNetworkMode = "host",
            MaxAllowedNetworkMode = "bridge",
        };

        CodeInterpreterOptionsValidator validator = new();
        ValidateOptionsResult result = validator.Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
        Assert.Contains("exceeds MaxAllowedNetworkMode", result.FailureMessage);
    }

    [Fact]
    public void AddTools_ShouldEmitAllowedNetworkModes_InCreateDockerSessionSchema()
    {
        TrackingDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(docker, new CodeInterpreterOptions
        {
            DefaultNetworkMode = "bridge",
            MaxAllowedNetworkMode = "bridge",
        });

        List<Chats.BE.Services.Models.ChatServices.OpenAI.ChatTool> tools = [];
        executor.AddTools(tools);

        Chats.BE.Services.Models.ChatServices.OpenAI.FunctionTool create = tools
            .OfType<Chats.BE.Services.Models.ChatServices.OpenAI.FunctionTool>()
            .Single(t => t.FunctionName == "create_docker_session");

        JsonObject schema = (JsonObject)JsonNode.Parse(create.FunctionParameters!)!;
        JsonObject props = (JsonObject)schema["properties"]!;
        JsonObject network = (JsonObject)props["networkMode"]!;
        string desc = network["description"]!.GetValue<string>();

        Assert.Contains("One of: none, bridge", desc);
        Assert.DoesNotContain("host", desc);
        Assert.DoesNotContain("{allowedNetworkModes}", desc);
    }

    [Fact]
    public async Task CreateDockerSession_ShouldReject_DisallowedHigherNetworkMode()
    {
        TrackingDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(docker, new CodeInterpreterOptions
        {
            DefaultNetworkMode = "bridge",
            MaxAllowedNetworkMode = "bridge",
        });

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> done = await executor.CreateDockerSession(
            ctx,
            image: null,
            label: null,
            memoryBytes: null,
            cpuCores: null,
            maxProcesses: null,
            networkMode: "host",
            cancellationToken: CancellationToken.None);

        Assert.True(done.IsFailure);
        Assert.Contains("Requested networkMode 'host' exceeds MaxAllowedNetworkMode 'bridge'", done.Error);
        Assert.False(docker.EnsureImageCalled);
        Assert.False(docker.CreateContainerCalled);
    }

    private static CodeInterpreterExecutor CreateExecutor(TrackingDockerService docker, CodeInterpreterOptions options)
    {
        return new CodeInterpreterExecutor(
            docker,
            fileServiceFactory: null!,
            scopeFactory: new ThrowingScopeFactory(),
            codePodConfig: Options.Create(new CodePodConfig()),
            options: Options.Create(options),
            logger: NullLogger<CodeInterpreterExecutor>.Instance);
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx()
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = Array.Empty<ChatTurn>(),
            MessageSteps = Array.Empty<Step>(),
            CurrentAssistantTurn = new ChatTurn { Id = 123, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("ScopeFactory should not be used in this test");
    }

    private sealed class TrackingDockerService : IDockerService
    {
        public bool EnsureImageCalled { get; private set; }
        public bool CreateContainerCalled { get; private set; }

        public void Dispose() { }

        public Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
        {
            EnsureImageCalled = true;
            throw new InvalidOperationException("Docker should not be called in this test");
        }

        public Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
        {
            CreateContainerCalled = true;
            throw new InvalidOperationException("Docker should not be called in this test");
        }

        public Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<string>> ListImagesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ContainerInfo?> GetContainerAsync(string containerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAllManagedContainersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
