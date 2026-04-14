using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterViewImageTests
{
    private static readonly byte[] ValidPngBytes = CreateValidPngBytes();

    private static ServiceProvider CreateServiceProvider(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static byte[] CreateValidPngBytes()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(255, 0, 0, 255);

        using MemoryStream ms = new();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static CodeInterpreterExecutor CreateExecutor(ServiceProvider sp, FakeDockerService docker, CodeInterpreterOptions? options = null)
    {
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        HostUrlService host = new(accessor);
        IFileServiceFactory fsf = new FileServiceFactory(host, new NoOpUrlEncryptionService());

        return new CodeInterpreterExecutor(
            docker,
            fsf,
            new FileImageInfoService(NullLogger<FileImageInfoService>.Instance),
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CodePodConfig()),
            Options.Create(options ?? new CodeInterpreterOptions()),
            NullLogger<CodeInterpreterExecutor>.Instance);
    }

    private static async Task<ChatDockerSession> SeedSessionAsync(ServiceProvider sp, string label, string containerId)
    {
        using IServiceScope scope = sp.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        db.ChatTurns.Add(new ChatTurn
        {
            Id = 1,
            ChatId = 1,
            ParentId = null,
            IsUser = false,
            Chat = null!,
        });

        DateTime now = DateTime.UtcNow;
        ChatDockerSession session = new()
        {
            OwnerTurnId = 1,
            Label = label,
            ContainerId = containerId,
            Image = "mcr.microsoft.com/dotnet/sdk:10.0",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = 0,
            CreatedAt = now.AddMinutes(-10),
            LastActiveAt = now.AddMinutes(-5),
            ExpiresAt = now.AddMinutes(30),
        };

        db.ChatDockerSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static CodeInterpreterExecutor.TurnContext CreateContext(ChatDockerSession session)
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = [new ChatTurn { Id = 1, ChatDockerSessions = [session] }],
            MessageSteps = [],
            CurrentAssistantTurn = new ChatTurn { Id = 1, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
    }

    [Fact]
    public void AddTools_ShouldHideViewImage_WhenVisionDisabled()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(AddTools_ShouldHideViewImage_WhenVisionDisabled));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        List<ChatTool> tools = [];
        executor.AddTools(tools, allowVision: false);

        Assert.DoesNotContain(tools.OfType<FunctionTool>(), x => x.FunctionName == CodeInterpreterExecutor.ViewImageToolName);
    }

    [Fact]
    public void AddTools_ShouldIncludeViewImage_WhenVisionEnabled()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(AddTools_ShouldIncludeViewImage_WhenVisionEnabled));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);

        List<ChatTool> tools = [];
        executor.AddTools(tools, allowVision: true);

        Assert.Contains(tools.OfType<FunctionTool>(), x => x.FunctionName == CodeInterpreterExecutor.ViewImageToolName);
    }

    [Fact]
    public async Task ViewImage_ShouldQueueArtifact_WhenImageIsValid()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(ViewImage_ShouldQueueArtifact_WhenImageIsValid));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);
        ChatDockerSession session = await SeedSessionAsync(sp, "s1", "container-123");
        CodeInterpreterExecutor.TurnContext ctx = CreateContext(session);
        docker.AddFile(session.ContainerId, "/app/chart.png", ValidPngBytes);

        Result<string> result = await executor.ViewImage(ctx, session.Label, "/app/chart.png", CancellationToken.None);
        List<CodeInterpreterExecutor.PendingFileArtifact> artifacts = executor.DrainPendingArtifacts(ctx);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value);
        CodeInterpreterExecutor.PendingFileArtifact artifact = Assert.Single(artifacts);
        Assert.Equal("chart.png", artifact.FileName);
        Assert.Equal("image/png", artifact.ContentType);
        Assert.Equal(ValidPngBytes, artifact.Bytes);
    }

    [Fact]
    public async Task ViewImage_ShouldRejectNonImageExtension()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(ViewImage_ShouldRejectNonImageExtension));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);
        ChatDockerSession session = await SeedSessionAsync(sp, "s1", "container-123");
        CodeInterpreterExecutor.TurnContext ctx = CreateContext(session);
        docker.AddFile(session.ContainerId, "/app/chart.txt", ValidPngBytes);

        Result<string> result = await executor.ViewImage(ctx, session.Label, "/app/chart.txt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not look like an image", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(ctx));
    }

    [Fact]
    public async Task ViewImage_ShouldRejectEmptyFile()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(ViewImage_ShouldRejectEmptyFile));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker);
        ChatDockerSession session = await SeedSessionAsync(sp, "s1", "container-123");
        CodeInterpreterExecutor.TurnContext ctx = CreateContext(session);
        docker.AddFile(session.ContainerId, "/app/chart.png", []);

        Result<string> result = await executor.ViewImage(ctx, session.Label, "/app/chart.png", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("is empty", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(ctx));
    }

    [Fact]
    public async Task ViewImage_ShouldRejectTooLargeImage()
    {
        using ServiceProvider sp = CreateServiceProvider(nameof(ViewImage_ShouldRejectTooLargeImage));
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CreateExecutor(sp, docker, new CodeInterpreterOptions { MaxSingleUploadBytes = 8 });
        ChatDockerSession session = await SeedSessionAsync(sp, "s1", "container-123");
        CodeInterpreterExecutor.TurnContext ctx = CreateContext(session);
        docker.AddFile(session.ContainerId, "/app/chart.png", ValidPngBytes);

        Result<string> result = await executor.ViewImage(ctx, session.Label, "/app/chart.png", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("too large", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(ctx));
    }
}