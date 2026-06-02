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

    private static CodeInterpreterExecutor CreateExecutor(ServiceProvider sp, FakeDockerService docker, CodePodConfig? codePodConfig = null)
    {
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        HostUrlService host = new(accessor);
        IFileServiceFactory fsf = new FileServiceFactory(host, new NoOpUrlEncryptionService());

        return new CodeInterpreterExecutor(
            docker,
            fsf,
            new FileImageInfoService(NullLogger<FileImageInfoService>.Instance),
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(codePodConfig ?? new CodePodConfig()),
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

    [Theory]
    [InlineData("multiple-lines", "line1\nline2\nline3", "Wrote 3 lines")]
    [InlineData("empty", "", "Wrote 0 lines")]
    [InlineData("single-line", "single line", "Wrote 1 lines")]
    [InlineData("crlf", "line1\r\nline2\r\nline3\r\nline4", "Wrote 4 lines")]
    public async Task WriteFile_ShouldReportLineCount(string caseName, string textContent, string expectedMessage)
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext ctx, string sessionLabel, _) = await CreateWriteFileScenarioAsync(caseName);

        Result<string> result = await executor.WriteFile(ctx, sessionLabel, "/app/test.txt", textContent, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(expectedMessage, result.Value);
        Assert.DoesNotContain("bytes", result.Value);
    }

    [Theory]
    [InlineData("relative", "test.py", "/workspace/test.py")]
    [InlineData("absolute", "/tmp/test.py", "/tmp/test.py")]
    public async Task WriteFile_ShouldResolvePathAgainstWorkDir(string caseName, string filePath, string expectedPath)
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext ctx, string sessionLabel, FakeDockerService docker) =
            await CreateWriteFileScenarioAsync(caseName, new CodePodConfig { WorkDir = "/workspace" });

        Result<string> result = await executor.WriteFile(ctx, sessionLabel, filePath, "print('hello')", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPath, docker.LastUploadedPath);
        Assert.Contains(expectedPath, result.Value);
    }

    private static async Task<(CodeInterpreterExecutor Executor, CodeInterpreterExecutor.TurnContext Context, string SessionLabel, FakeDockerService Docker)>
        CreateWriteFileScenarioAsync(string dbName, CodePodConfig? codePodConfig = null)
    {
        ServiceProvider sp = CreateServiceProvider($"{nameof(CodeInterpreterWriteFileTests)}_{dbName}");
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker, codePodConfig);

        const string sessionLabel = "s1";
        ChatDockerSession session = await SeedSessionAsync(sp, ownerTurnId: 1, sessionLabel, "container-123");

        CodeInterpreterExecutor.TurnContext ctx = new()
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1 },
            ClientInfoId = 1
        };

        return (executor, ctx, sessionLabel, docker);
    }
}
