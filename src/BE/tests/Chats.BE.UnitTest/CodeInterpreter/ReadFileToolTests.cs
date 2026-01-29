using System.Text;

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

public sealed class ReadFileToolTests
{
    private sealed class FakeDockerService : IDockerService
    {
        private readonly Dictionary<(string containerId, string filePath), byte[]> _files = new();

        public string? LastDownloadContainerId { get; private set; }
        public string? LastDownloadPath { get; private set; }

        public CodePodConfig Config { get; } = new();

        /// <summary>
        /// Normalize path: relative paths like "foo.txt" become "/app/foo.txt".
        /// Absolute paths (starting with "/") are kept as-is.
        /// </summary>
        private string NormalizePath(string path)
        {
            if (path.StartsWith('/'))
            {
                return path; // absolute path, keep as-is
            }
            // relative path -> prepend workDir
            string workDir = Config.WorkDir; // default "/app"
            return $"{workDir}/{path}";
        }

        public void AddFile(string containerId, string filePath, byte[] content)
        {
            string normalized = NormalizePath(filePath);
            _files[(containerId, normalized)] = content;
        }

        public void Dispose() { }

        public Task EnsureImageAsync(string image, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ContainerInfo> CreateContainerAsync(string image, ResourceLimits? resourceLimits = null, NetworkMode? networkMode = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<ContainerInfo>> GetManagedContainersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ContainerInfo>());

        public Task<List<string>> ListImagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

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
        {
            string normalized = NormalizePath(filePath);
            LastDownloadContainerId = containerId;
            LastDownloadPath = normalized;
            if (_files.TryGetValue((containerId, normalized), out byte[]? content))
            {
                return Task.FromResult(content);
            }

            throw new InvalidOperationException($"File not found in fake docker: {containerId}:{normalized}");
        }

        public Task<SessionUsage?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
            => Task.FromResult<SessionUsage?>(null);
    }

    private static ServiceProvider CreateServiceProvider(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static CodeInterpreterExecutor CreateExecutor(ServiceProvider sp, FakeDockerService docker, OutputOptions? outputOptions = null)
    {
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        HostUrlService host = new(accessor);
        IFileServiceFactory fsf = new FileServiceFactory(host, new NoOpUrlEncryptionService());

        CodePodConfig podConfig = new();
        if (outputOptions != null)
        {
            podConfig.OutputOptions = outputOptions;
        }

        return new CodeInterpreterExecutor(
            docker,
            fsf,
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(podConfig),
            Options.Create(new CodeInterpreterOptions()),
            NullLogger<CodeInterpreterExecutor>.Instance);
    }

    private static async Task<ChatDockerSession> SeedSessionAsync(ServiceProvider sp, long ownerTurnId, string label, string containerId)
    {
        using IServiceScope scope = sp.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        db.ChatTurns.Add(new ChatTurn
        {
            Id = ownerTurnId,
            ChatId = 1,
            ParentId = null,
            IsUser = false,
            Chat = null!,
        });

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

        // Return a detached instance.
        return await db.ChatDockerSessions.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx(long currentTurnId)
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = Array.Empty<ChatTurn>(),
            MessageSteps = Array.Empty<Step>(),
            CurrentAssistantTurn = new ChatTurn { Id = currentTurnId, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
    }

    private static void AddSessionToCtx(CodeInterpreterExecutor.TurnContext ctx, string sessionId, ChatDockerSession dbSession)
    {
        ctx.SessionsBySessionId[sessionId] = new CodeInterpreterExecutor.TurnContext.SessionState
        {
            DbSession = dbSession,
            ShellPrefix = dbSession.ShellPrefix.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SnapshotTaken = true,
            UsedInThisTurn = false,
        };
    }

    [Fact]
    public async Task ReadFile_Defaults_ReturnsAllLines()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: null, endLine: null, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal("a\nb\nc", r.Value);
        Assert.Equal("c1", docker.LastDownloadContainerId);
        Assert.Equal("/app/foo.txt", docker.LastDownloadPath);
    }

    [Fact]
    public async Task ReadFile_LineRange_ReturnsSubset()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc\nd"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 2, endLine: 3, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal("b\nc", r.Value);
    }

    [Fact]
    public async Task ReadFile_EndLineBeyondEof_ClampsToEof()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 2, endLine: 999, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal("b\nc", r.Value);
    }

    [Fact]
    public async Task ReadFile_StartLineBeyondEof_ReturnsEmpty_WhenWithLineNumbers()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 999, endLine: null, withLineNumbers: true, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal("TotalLines: 3", r.Value);
    }

    [Fact]
    public async Task ReadFile_WithLineNumbers_IncludesTotalLines_AndPrefixes()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 2, endLine: 3, withLineNumbers: true, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal("TotalLines: 3\n2: b\n3: c", r.Value);
    }

    [Fact]
    public async Task ReadFile_InvalidStartLine_ReturnsFail()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 0, endLine: null, withLineNumbers: null, CancellationToken.None);

        Assert.False(r.IsSuccess);
        Assert.Contains("startLine", r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadFile_EndLineLessThanStartLine_ReturnsFail()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();
        CodeInterpreterExecutor exec = CreateExecutor(sp, docker);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes("a\nb\nc"));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: 3, endLine: 2, withLineNumbers: null, CancellationToken.None);

        Assert.False(r.IsSuccess);
        Assert.Contains("endLine", r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadFile_Truncation_Head_AppendsNote()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 40,
            Strategy = TruncationStrategy.Head,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        string content = new string('A', 200);
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes(content));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: null, endLine: null, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Contains("lines omitted", r.Value, StringComparison.Ordinal);
        Assert.EndsWith("lines omitted] ...\n", r.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_Truncation_Tail_PrependsNote()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 40,
            Strategy = TruncationStrategy.Tail,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        string content = "HEAD-" + new string('B', 200) + "-TAIL";
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes(content));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: null, endLine: null, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        int noteIdx = r.Value.IndexOf("lines omitted", StringComparison.Ordinal);
        Assert.True(noteIdx >= 0 && noteIdx < 30);
        Assert.Contains("-TAIL", r.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_Truncation_HeadAndTail_InsertsMiddle()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 60,
            Strategy = TruncationStrategy.HeadAndTail,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        string head = "HEAD-" + new string('C', 50);
        string tail = new string('D', 50) + "-TAIL";
        string content = head + new string('X', 500) + tail;
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes(content));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: null, endLine: null, withLineNumbers: null, CancellationToken.None);

        Assert.True(r.IsSuccess);
        int headIdx = r.Value.IndexOf("HEAD-", StringComparison.Ordinal);
        int noteIdx = r.Value.IndexOf("lines omitted", StringComparison.Ordinal);
        int tailIdx = r.Value.IndexOf("-TAIL", StringComparison.Ordinal);
        Assert.True(headIdx >= 0);
        Assert.True(noteIdx > headIdx);
        Assert.True(tailIdx > noteIdx);
    }

    [Fact]
    public async Task ReadFile_WithLineNumbers_PreservesTotalLinesPrefix_EvenWhenTailTruncates()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 45,
            Strategy = TruncationStrategy.Tail,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        string content = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"line-{i:D3}-" + new string('Z', 10)));
        docker.AddFile("c1", "foo.txt", Encoding.UTF8.GetBytes(content));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "foo.txt", startLine: null, endLine: null, withLineNumbers: true, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.StartsWith("TotalLines: 100\n", r.Value, StringComparison.Ordinal);

        int prefixEnd = r.Value.IndexOf('\n');
        int noteIdx = r.Value.IndexOf("lines omitted", StringComparison.Ordinal);
        Assert.True(noteIdx > prefixEnd);
    }

    [Fact]
    public async Task ReadFile_Binary_FallsBackToBase64Preview_AndSupportsWithLineNumbersPrefix()
    {
        using ServiceProvider sp = CreateServiceProvider(Guid.NewGuid().ToString());
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 256,
            Strategy = TruncationStrategy.Head,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        byte[] bytes = [0xFF, 0xFE, 0xFD, 0xFC, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05];
        docker.AddFile("c1", "bin.dat", bytes);

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "bin.dat", startLine: null, endLine: null, withLineNumbers: true, CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.StartsWith("TotalLines: 0\nPath: bin.dat\n", r.Value, StringComparison.Ordinal);
        Assert.Contains("Base64(first 10 bytes):", r.Value, StringComparison.Ordinal);
        Assert.Contains(Convert.ToBase64String(bytes), r.Value, StringComparison.Ordinal);
    }
}
