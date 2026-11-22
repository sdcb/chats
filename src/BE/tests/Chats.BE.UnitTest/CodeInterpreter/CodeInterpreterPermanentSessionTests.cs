using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterPermanentSessionTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task CachedPermanentSession_UsesCurrentBackendId_AfterRecreation()
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource cached, ServiceProvider sp) =
            await CreateScenarioAsync(nameof(CachedPermanentSession_UsesCurrentBackendId_AfterRecreation));
        using (sp)
        {
            await SetBackendIdAsync(sp, cached.Id, "new-id");
            docker.AddFile("new-id", "/app/a.png", Png);

            Result<string> result = await executor.ViewImage(context, cached.Name, "/app/a.png", CancellationToken.None);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal("new-id", docker.LastDownloadContainerId);
            Assert.Equal("new-id", context.SessionsBySessionId[cached.Name].DbSession.ContainerId);
        }
    }

    [Fact]
    public async Task DestroySession_RefusesPermanentContainer_EvenWithStaleBackendId()
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource cached, ServiceProvider sp) =
            await CreateScenarioAsync(nameof(DestroySession_RefusesPermanentContainer_EvenWithStaleBackendId));
        using (sp)
        {
            await SetBackendIdAsync(sp, cached.Id, "new-id");

            Result<string> result = await executor.DestroySession(context, cached.Name, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(docker.DeletedContainers);
            using IServiceScope scope = sp.CreateScope();
            Assert.Null((await scope.ServiceProvider.GetRequiredService<ChatsDB>().ContainerResources.SingleAsync()).DeletedAt);
        }
    }

    private static async Task<(CodeInterpreterExecutor, CodeInterpreterExecutor.TurnContext, FakeDockerService, ContainerResource, ServiceProvider)> CreateScenarioAsync(string name)
    {
        ServiceProvider sp = CodeInterpreterToolTestHelper.CreateServiceProvider(name);
        ContainerResource seeded = await CodeInterpreterToolTestHelper.SeedResourceAsync(sp, "site", "old-id");
        using (IServiceScope scope = sp.CreateScope())
        {
            ChatsDB seedDb = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            ContainerResource r = await seedDb.ContainerResources.SingleAsync();
            r.IsPermanent = true;
            r.CleanupAt = null;
            await seedDb.SaveChangesAsync();
        }
        seeded.IsPermanent = true;
        seeded.CleanupAt = null;

        FakeDockerService docker = new();
        ChatsDB db = sp.CreateScope().ServiceProvider.GetRequiredService<ChatsDB>();
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(sp, docker, db: db);
        return (executor, CodeInterpreterToolTestHelper.CreateContext(seeded), docker, seeded, sp);
    }

    private static async Task SetBackendIdAsync(ServiceProvider sp, long id, string backendId)
    {
        using IServiceScope scope = sp.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        (await db.ContainerResources.SingleAsync(x => x.Id == id)).BackendResourceId = backendId;
        await db.SaveChangesAsync();
    }
}
