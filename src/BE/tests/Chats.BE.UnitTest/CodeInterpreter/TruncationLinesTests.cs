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

public sealed class TruncationLinesTests
{
    private sealed class FakeDockerService : IDockerService
    {
        private readonly Dictionary<(string containerId, string filePath), byte[]> _files = new();

        public CodePodConfig Config { get; } = new();

        public void AddFile(string containerId, string filePath, byte[] content)
        {
            _files[(containerId, filePath)] = content;
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
            if (_files.TryGetValue((containerId, filePath), out byte[]? content))
            {
                return Task.FromResult(content);
            }

            throw new InvalidOperationException($"File not found in fake docker: {containerId}:{filePath}");
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

        return await db.ChatDockerSessions.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx(long currentTurnId)
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = [],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = currentTurnId },
            ClientInfoId = 1
        };
    }

    private static void AddSessionToCtx(CodeInterpreterExecutor.TurnContext ctx, string label, ChatDockerSession session)
    {
        ctx.SessionsBySessionId[label] = new CodeInterpreterExecutor.TurnContext.SessionState
        {
            DbSession = session,
            ShellPrefix = ["/bin/sh", "-lc"],
            SnapshotTaken = true,
        };
    }

    [Fact]
    public async Task ReadFile_TruncationShowsLinesNotBytes()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProvider(nameof(ReadFile_TruncationShowsLinesNotBytes));
        FakeDockerService docker = new();

        OutputOptions oo = new()
        {
            MaxOutputBytes = 50, // Small limit to force truncation
            Strategy = TruncationStrategy.Head,
        };

        CodeInterpreterExecutor exec = CreateExecutor(sp, docker, oo);

        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, label: "s", containerId: "c1");
        
        // Create content with 10 lines, each 20 bytes (200 bytes total)
        string content = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"Line {i:D2} - Data"));
        docker.AddFile("c1", "test.txt", Encoding.UTF8.GetBytes(content));

        CodeInterpreterExecutor.TurnContext ctx = CreateCtx(currentTurnId: 1);
        AddSessionToCtx(ctx, "s", session);

        // Act
        Result<string> r = await exec.ReadFile(ctx, sessionId: "s", path: "test.txt", startLine: null, endLine: null, withLineNumbers: null, CancellationToken.None);

        // Assert
        Assert.True(r.IsSuccess);
        Assert.Contains("lines omitted", r.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes omitted", r.Value, StringComparison.Ordinal);
        
        // Extract the number from the truncation message
        int linesIdx = r.Value.IndexOf("lines omitted", StringComparison.Ordinal);
        int colonIdx = r.Value.LastIndexOf(':', linesIdx);
        string numberPart = r.Value[(colonIdx + 1)..linesIdx].Trim();
        
        Assert.True(int.TryParse(numberPart, out int omittedLines));
        Assert.True(omittedLines > 0, "Should show positive number of omitted lines");
    }

    [Fact]
    public void CommandOutputTruncation_ShowsLinesNotBytes()
    {
        // Arrange
        string content = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Output line {i}"));
        
        OutputOptions options = new()
        {
            MaxOutputBytes = 100,
            Strategy = TruncationStrategy.HeadAndTail,
        };

        // Act
        (string truncatedOutput, bool truncated) = CommandOutputTruncation.Truncate(content, options);

        // Assert
        Assert.True(truncated);
        Assert.Contains("lines omitted", truncatedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes omitted", truncatedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandOutputTruncation_CountsLinesCorrectly()
    {
        // Arrange: Create content with exactly 10 lines
        string content = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"Line {i}"));
        
        OutputOptions options = new()
        {
            MaxOutputBytes = 30, // Very small to force aggressive truncation
            Strategy = TruncationStrategy.HeadAndTail,
        };

        // Act
        (string truncatedOutput, bool truncated) = CommandOutputTruncation.Truncate(content, options);

        // Assert
        Assert.True(truncated);
        
        // Extract and verify the omitted lines count
        int omittedIdx = truncatedOutput.IndexOf("lines omitted", StringComparison.Ordinal);
        Assert.True(omittedIdx > 0, "Should contain 'lines omitted' message");
        
        // The message format is: "\n... [Output truncated: X lines omitted] ...\n"
        int startIdx = truncatedOutput.LastIndexOf('[', omittedIdx);
        int colonIdx = truncatedOutput.IndexOf(':', startIdx);
        string numberPart = truncatedOutput[(colonIdx + 1)..omittedIdx].Trim();
        
        Assert.True(int.TryParse(numberPart, out int omittedLines));
        Assert.True(omittedLines > 0 && omittedLines <= 10, $"Omitted lines should be between 1-10, got {omittedLines}");
    }
}
