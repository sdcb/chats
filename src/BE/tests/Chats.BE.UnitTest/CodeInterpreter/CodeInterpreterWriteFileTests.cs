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

public sealed class CodeInterpreterWriteFileTests
{
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

    [Fact]
    public async Task WriteFile_ReturnsLineCount_NotByteCount()
    {
        // Arrange
        ServiceProvider sp = CreateServiceProvider(nameof(WriteFile_ReturnsLineCount_NotByteCount));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        const string sessionLabel = "s1";
        const string containerId = "container-123";
        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, sessionLabel, containerId);

        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1 },
            ClientInfoId = 1
        };

        // Act: Write file with multiple lines
        string textContent = "line1\nline2\nline3";
        Result<string> result = await executor.WriteFile(
            ctx,
            sessionLabel,
            "/app/test.txt",
            textContent,
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Wrote 3 lines", result.Value);
        Assert.DoesNotContain("bytes", result.Value);
    }

    [Fact]
    public async Task WriteFile_EmptyText_Returns0Lines()
    {
        // Arrange
        ServiceProvider sp = CreateServiceProvider(nameof(WriteFile_EmptyText_Returns0Lines));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        const string sessionLabel = "s1";
        const string containerId = "container-123";
        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, sessionLabel, containerId);

        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1 },
            ClientInfoId = 1
        };

        // Act: Write empty file
        Result<string> result = await executor.WriteFile(
            ctx,
            sessionLabel,
            "/app/empty.txt",
            "",
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Wrote 0 lines", result.Value);
    }

    [Fact]
    public async Task WriteFile_SingleLine_Returns1Line()
    {
        // Arrange
        ServiceProvider sp = CreateServiceProvider(nameof(WriteFile_SingleLine_Returns1Line));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        const string sessionLabel = "s1";
        const string containerId = "container-123";
        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, sessionLabel, containerId);

        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1 },
            ClientInfoId = 1
        };

        // Act: Write single line file
        Result<string> result = await executor.WriteFile(
            ctx,
            sessionLabel,
            "/app/single.txt",
            "single line",
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Wrote 1 lines", result.Value);
    }

    [Fact]
    public async Task WriteFile_MultiLineWithCRLF_CountsCorrectly()
    {
        // Arrange
        ServiceProvider sp = CreateServiceProvider(nameof(WriteFile_MultiLineWithCRLF_CountsCorrectly));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        const string sessionLabel = "s1";
        const string containerId = "container-123";
        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, sessionLabel, containerId);

        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1 },
            ClientInfoId = 1
        };

        // Act: Write file with CRLF line endings
        string textContent = "line1\r\nline2\r\nline3\r\nline4";
        Result<string> result = await executor.WriteFile(
            ctx,
            sessionLabel,
            "/app/crlf.txt",
            textContent,
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Wrote 4 lines", result.Value);
    }
}
