using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterSessionLookupTests
{
    private sealed class FakeDockerService : IDockerService
    {
        public int CreateContainerCalls { get; private set; }

        public void Dispose() { }

        public Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
        {
            CreateContainerCalls++;
            return Task.FromResult(new ContainerInfo
            {
                ContainerId = $"container-{CreateContainerCalls:D2}-abcdef0123456789",
                Name = $"codepod-test-{CreateContainerCalls:D2}",
                Image = image,
                DockerStatus = "running",
                CreatedAt = DateTimeOffset.UtcNow,
                ShellPrefix = ["/bin/sh", "-lc"],
            });
        }

        public Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ContainerInfo>());

        public Task<ContainerInfo?> GetContainerAsync(string containerId, CancellationToken cancellationToken = default)
            => Task.FromResult<ContainerInfo?>(null);

        public Task DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAllManagedContainersAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CommandExitEvent> ExecuteCommandAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] shellPrefix, string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<CommandOutputEvent> ExecuteCommandStreamAsync(string containerId, string[] command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UploadFileAsync(string containerId, string containerPath, byte[] content, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<FileEntry>());

        public Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
            => Task.FromResult<SessionUsage?>(null);
    }

    private static ServiceProvider CreateServiceProvider(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static CodeInterpreterExecutor CreateExecutor(ServiceProvider sp, FakeDockerService docker)
    {
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        HostUrlService host = new(accessor);
        IFileServiceFactory fsf = new FileServiceFactory(host, new NoOpUrlEncryptionService());

        return new CodeInterpreterExecutor(
            docker,
            fsf,
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CodePodConfig()),
            Options.Create(new CodeInterpreterOptions()),
            NullLogger<CodeInterpreterExecutor>.Instance);
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx(long currentTurnId, params ChatTurn[] messageTurns)
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = messageTurns,
            MessageSteps = Array.Empty<Step>(),
            CurrentAssistantTurn = new ChatTurn { Id = currentTurnId, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
    }

    private static async Task SeedTurnAsync(ChatsDB db, long id, long? parentId)
    {
        db.ChatTurns.Add(new ChatTurn
        {
            Id = id,
            ChatId = 1,
            ParentId = parentId,
            IsUser = false,
            Chat = null!,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ChatDockerSession> SeedSessionAsync(ChatsDB db, long ownerTurnId, string label, string containerId)
    {
        DateTime now = DateTime.UtcNow;
        ChatDockerSession session = new()
        {
            OwnerTurnId = ownerTurnId,
            Label = label,
            ContainerId = containerId,
            Image = "mcr.microsoft.com/dotnet/sdk:10.0",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now.AddMinutes(-10),
            LastActiveAt = now.AddMinutes(-5),
            ExpiresAt = now.AddMinutes(30),
        };
        db.ChatDockerSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    [Fact]
    public async Task CreateSession_ShouldReuseNearestAncestorSession_ByLabel()
    {
        string dbName = Guid.NewGuid().ToString();
        using ServiceProvider sp = CreateServiceProvider(dbName);
        ChatDockerSession containerRoot = null!, containerA = null!;
        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            await SeedTurnAsync(db, id: 1, parentId: null);
            await SeedTurnAsync(db, id: 2, parentId: 1); // Turn A
            await SeedTurnAsync(db, id: 4, parentId: 2); // Turn C (child of A)

            containerRoot = await SeedSessionAsync(db, ownerTurnId: 1, label: "dotnet-env", containerId: "container-root");
            containerA = await SeedSessionAsync(db, ownerTurnId: 2, label: "dotnet-env", containerId: "container-A");
        }

        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(
            currentTurnId: 4,
            new ChatTurn { Id = 1, ParentId = null, ChatId = 1, ChatDockerSessions = [containerRoot] },
            new ChatTurn { Id = 2, ParentId = 1, ChatId = 1, ChatDockerSessions = [containerA] },
            new ChatTurn { Id = 4, ParentId = 2, ChatId = 1 });

        Result<string> done = await exec.CreateDockerSession(
            ctx,
            image: null,
            label: "dotnet-env",
            memoryBytes: null,
            cpuCores: null,
            maxProcesses: null,
            networkMode: null,
            cancellationToken: CancellationToken.None);

        Assert.True(done.IsSuccess);
        Assert.Contains("sessionId: dotnet-env", done.Value);
        Assert.Contains("image: mcr.microsoft.com/dotnet/sdk:10.0", done.Value);
        Assert.Equal(0, docker.CreateContainerCalls);
    }

    [Fact]
    public async Task CreateSession_ShouldNotSeeSiblingSessions_AndCreateNew()
    {
        string dbName = Guid.NewGuid().ToString();
        using ServiceProvider sp = CreateServiceProvider(dbName);
        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            await SeedTurnAsync(db, id: 1, parentId: null);
            await SeedTurnAsync(db, id: 2, parentId: 1); // Turn A
            await SeedTurnAsync(db, id: 3, parentId: 1); // Turn A' (sibling)

            await SeedSessionAsync(db, ownerTurnId: 2, label: "dotnet-env", containerId: "container-A");
        }

        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(
            currentTurnId: 3,
            new ChatTurn { Id = 1, ParentId = null, ChatId = 1, Chat = null! },
            new ChatTurn { Id = 3, ParentId = 1, ChatId = 1, Chat = null! });

        Result<string> done = await exec.CreateDockerSession(
            ctx,
            image: null,
            label: "dotnet-env",
            memoryBytes: null,
            cpuCores: null,
            maxProcesses: null,
            networkMode: null,
            cancellationToken: CancellationToken.None);

        Assert.True(done.IsSuccess);
        Assert.Contains("sessionId: dotnet-env", done.Value);
        Assert.Contains("image: mcr.microsoft.com/dotnet/sdk:10.0", done.Value);
        Assert.Equal(1, docker.CreateContainerCalls);

        using (IServiceScope scope2 = sp.CreateScope())
        {
            ChatsDB db2 = scope2.ServiceProvider.GetRequiredService<ChatsDB>();
            ChatDockerSession created = await db2.ChatDockerSessions.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
            Assert.Equal(3, created.OwnerTurnId);
            Assert.Equal("dotnet-env", created.Label);
            Assert.Equal("container-01-abcdef0123456789", created.ContainerId);
        }
    }

    [Fact]
    public async Task EnsureSession_ShouldFail_WhenSessionWasDestroyed()
    {
        string dbName = Guid.NewGuid().ToString();
        using ServiceProvider sp = CreateServiceProvider(dbName);
        ChatDockerSession destroyedSession = null!;
        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            await SeedTurnAsync(db, id: 1, parentId: null);
            await SeedTurnAsync(db, id: 2, parentId: 1);

            destroyedSession = await SeedSessionAsync(db, ownerTurnId: 1, label: "destroyed-session", containerId: "container-destroyed");
            destroyedSession.TerminatedAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(
            currentTurnId: 2,
            new ChatTurn { Id = 1, ParentId = null, ChatId = 1, ChatDockerSessions = [destroyedSession] },
            new ChatTurn { Id = 2, ParentId = 1, ChatId = 1 });

        ToolProgressDelta result = await exec.RunCommand(
            ctx,
            sessionId: "destroyed-session",
            command: "echo test",
            timeoutSeconds: null,
            cancellationToken: CancellationToken.None).LastAsync();

        Assert.IsType<ToolCompletedToolProgressDelta>(result);
        ToolCompletedToolProgressDelta completed = (ToolCompletedToolProgressDelta)result;
        Assert.False(completed.Result.IsSuccess);
        Assert.Contains("destroyed-session", completed.Result.Error);
        Assert.Contains("was destroyed", completed.Result.Error);
    }

    [Fact]
    public async Task EnsureSession_ShouldFail_WhenSessionHasExpired()
    {
        string dbName = Guid.NewGuid().ToString();
        using ServiceProvider sp = CreateServiceProvider(dbName);
        ChatDockerSession expiredSession = null!;
        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            await SeedTurnAsync(db, id: 1, parentId: null);
            await SeedTurnAsync(db, id: 2, parentId: 1);

            expiredSession = await SeedSessionAsync(db, ownerTurnId: 1, label: "expired-session", containerId: "container-expired");
            expiredSession.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(
            currentTurnId: 2,
            new ChatTurn { Id = 1, ParentId = null, ChatId = 1, ChatDockerSessions = [expiredSession] },
            new ChatTurn { Id = 2, ParentId = 1, ChatId = 1 });

        ToolProgressDelta result = await exec.RunCommand(
            ctx,
            sessionId: "expired-session",
            command: "echo test",
            timeoutSeconds: null,
            cancellationToken: CancellationToken.None).LastAsync();

        Assert.IsType<ToolCompletedToolProgressDelta>(result);
        ToolCompletedToolProgressDelta completed = (ToolCompletedToolProgressDelta)result;
        Assert.False(completed.Result.IsSuccess);
        Assert.Contains("expired-session", completed.Result.Error);
        Assert.Contains("expired", completed.Result.Error);
    }

}
