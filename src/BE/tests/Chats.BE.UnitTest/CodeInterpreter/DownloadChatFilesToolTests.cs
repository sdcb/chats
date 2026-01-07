
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DB.Enums;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class DownloadChatFilesToolTests
{
    private sealed record UploadCall(string ContainerId, string Path, byte[] Content);

    private sealed class FakeFileServiceFactory(IReadOnlyDictionary<string, byte[]> blobs) : IFileServiceFactory
    {
        public IFileService Create(FileService dbfs) => new InMemoryFileService(blobs);
    }

    private sealed class InMemoryFileService(IReadOnlyDictionary<string, byte[]> blobs) : IFileService
    {
        public Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Stream> Download(string storageKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!blobs.TryGetValue(storageKey, out byte[]? bytes))
            {
                throw new FileNotFoundException($"Missing storageKey: {storageKey}");
            }
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }

        public string CreateDownloadUrl(CreateDownloadUrlRequest request)
            => throw new NotImplementedException();

        public Task<bool> Delete(string storageKey, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeDockerService : IDockerService
    {
        public List<UploadCall> Uploads { get; } = [];

        public void Dispose() { }

        public Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

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
        {
            Uploads.Add(new UploadCall(containerId, containerPath, content));
            return Task.CompletedTask;
        }

        public Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<FileEntry>());

        public Task<byte[]> DownloadFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
            => Task.FromResult<SessionUsage?>(null);
    }

    private sealed class ThrowingUrlEncryptionService : IUrlEncryptionService
    {
        public int DecryptAsInt32(string encrypted, EncryptionPurpose purpose) => throw new NotImplementedException();
        public long DecryptAsInt64(string encrypted, EncryptionPurpose purpose) => throw new NotImplementedException();
        public string Encrypt(int id, EncryptionPurpose purpose) => throw new NotImplementedException();
        public string Encrypt(long id, EncryptionPurpose purpose) => throw new NotImplementedException();
        public string CreateSignedPath(TimedId timedId, EncryptionPurpose purpose) => throw new NotImplementedException();
        public Result<int> DecodeSignedPathAsInt32(string path, long validBefore, string hash, EncryptionPurpose purpose) => throw new NotImplementedException();
    }

    private static ServiceProvider CreateServiceProvider(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static CodeInterpreterExecutor CreateExecutor(ServiceProvider sp, FakeDockerService docker, IReadOnlyDictionary<string, byte[]> blobs)
    {
        IFileServiceFactory fsf = new FakeFileServiceFactory(blobs);

        return new CodeInterpreterExecutor(
            docker,
            fsf,
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CodePodConfig()),
            Options.Create(new CodeInterpreterOptions()),
            NullLogger<CodeInterpreterExecutor>.Instance);
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx(ChatDockerSession session, Step[] steps)
    {
        ChatTurn assistantTurn = new() { Id = 123, ChatId = 1, Chat = null! };
        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = Array.Empty<ChatTurn>(),
            MessageSteps = steps,
            CurrentAssistantTurn = assistantTurn,
            ClientInfoId = 1,
        };

        ctx.SessionsBySessionId[session.Label] = new CodeInterpreterExecutor.TurnContext.SessionState
        {
            DbSession = session,
            ShellPrefix = ["/bin/sh", "-lc"],
            UsedInThisTurn = false,
            ArtifactsSnapshot = new Dictionary<string, FileEntry>(StringComparer.Ordinal),
            SnapshotTaken = true,
        };

        return ctx;
    }

    [Fact]
    public async Task DownloadChatFiles_ShouldOnlyListAndUploadMatchedFiles()
    {
        string dbName = Guid.NewGuid().ToString();
        using ServiceProvider sp = CreateServiceProvider(dbName);

        FileService localFs = new()
        {
            Id = 1,
            FileServiceTypeId = (byte)DBFileServiceType.Local,
            Name = "local",
            Configs = "in-memory",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        byte[] zipBytes = [0x50, 0x4B, 0x03, 0x04, 0x00];
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D];

        string zipStorageKey = "maze_game.zip";
        string pngStorageKey = "maze.png";
        Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal)
        {
            [zipStorageKey] = zipBytes,
            [pngStorageKey] = pngBytes,
        };

        DBFile zip = new()
        {
            Id = 1,
            FileName = "maze_game.zip",
            StorageKey = zipStorageKey,
            Size = zipBytes.Length,
            MediaType = "application/zip",
            FileServiceId = localFs.Id,
            FileService = localFs,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = DateTime.UtcNow,
            ClientInfo = null!,
            CreateUser = null!,
        };

        DBFile png = new()
        {
            Id = 2,
            FileName = "maze.png",
            StorageKey = pngStorageKey,
            Size = pngBytes.Length,
            MediaType = "image/png",
            FileServiceId = localFs.Id,
            FileService = localFs,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = DateTime.UtcNow,
            ClientInfo = null!,
            CreateUser = null!,
        };

        Step step = new()
        {
            TurnId = 1,
            ChatRoleId = 1,
            CreatedAt = DateTime.UtcNow,
            Turn = new ChatTurn { Id = 1, ChatId = 1, Chat = null! },
            StepContents = new List<StepContent>
                {
                    StepContent.FromFile(png),
                    StepContent.FromFile(zip),
                }
        };

        DateTime now = DateTime.UtcNow;
        ChatDockerSession session = new()
        {
            Id = 1,
            Label = "s1",
            ContainerId = "container-1",
            Image = "mcr.microsoft.com/dotnet/sdk:10.0",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            db.ChatDockerSessions.Add(session);
            await db.SaveChangesAsync();
        }

        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, blobs);
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(session, [step]);

        Result<string> done = await exec.DownloadChatFiles(ctx, session.Label, ["maze_game.zip"], CancellationToken.None);

        Assert.True(done.IsSuccess);
        Assert.Contains("maze_game.zip", done.Value);
        Assert.DoesNotContain("maze.png", done.Value);

        Assert.Single(docker.Uploads);
        Assert.Equal("container-1", docker.Uploads[0].ContainerId);
        Assert.EndsWith("maze_game.zip", docker.Uploads[0].Path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(zipBytes, docker.Uploads[0].Content);
    }
}
