using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.FileServices;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DockerInterface;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.CodeInterpreter;

internal static class CodeInterpreterToolTestHelper
{
    public static ServiceProvider CreateServiceProvider(string databaseName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    public static CodeInterpreterExecutor CreateExecutor(
        ServiceProvider serviceProvider,
        FakeDockerService docker,
        CodePodConfig? codePodConfig = null,
        CodeInterpreterOptions? options = null,
        IFileServiceFactory? fileServiceFactory = null)
    {
        if (fileServiceFactory is null)
        {
            IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            HostUrlService host = new(accessor);
            fileServiceFactory = new FileServiceFactory(host, new NoOpUrlEncryptionService());
        }

        return new CodeInterpreterExecutor(
            docker,
            fileServiceFactory,
            new FileImageInfoService(NullLogger<FileImageInfoService>.Instance),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(codePodConfig ?? new CodePodConfig()),
            Options.Create(options ?? new CodeInterpreterOptions()),
            NullLogger<CodeInterpreterExecutor>.Instance);
    }

    public static async Task<ContainerResource> SeedResourceAsync(
        ServiceProvider serviceProvider,
        string name,
        string backendResourceId,
        long ownerTurnId = 1)
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource resource = new()
        {
            Id = ownerTurnId,
            OwnerUserId = 1,
            OwnerChatId = 1,
            OwnerTurnId = ownerTurnId,
            RuntimeNodeId = 1,
            IsPermanent = false,
            BackendResourceId = backendResourceId,
            Name = name,
            Image = "mcr.microsoft.com/dotnet/sdk:10.0",
            ShellPrefix = "/bin/sh,-lc",
            BackendNetworkName = "none",
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-5),
            LastActiveAt = now.AddMinutes(-5),
            CleanupAt = now.AddMinutes(30),
        };

        using IServiceScope scope = serviceProvider.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        db.ContainerResources.Add(resource);
        await db.SaveChangesAsync();
        return resource;
    }

    public static CodeInterpreterExecutor.TurnContext CreateContext(
        ContainerResource resource,
        IReadOnlyList<Step>? steps = null)
    {
        CodeInterpreterExecutor.TurnContext context = new()
        {
            MessageTurns = [new ChatTurn { Id = resource.OwnerTurnId ?? 1, ChatId = 1, ContainerResources = [resource] }],
            MessageSteps = steps ?? [],
            CurrentAssistantTurn = new ChatTurn { Id = resource.OwnerTurnId ?? 1, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
        context.SessionsBySessionId[resource.Name] = new CodeInterpreterExecutor.TurnContext.SessionState
        {
            DbSession = new CodeInterpreterExecutor.ContainerExecutionContext { Resource = resource },
            ShellPrefix = ["/bin/sh", "-lc"],
            SnapshotTaken = true,
        };
        return context;
    }
}
