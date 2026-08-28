using Chats.BE.Services.CodeInterpreter;
using Chats.DB;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterSessionLookupTests
{
    [Fact]
    public void CollectActiveSessions_ExcludesStoppedDeletedAndExpiredResources()
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource running = New(1, "running", now.AddMinutes(1));
        ContainerResource stopped = New(2, "stopped", now.AddMinutes(1));
        stopped.StoppedAt = now;
        ContainerResource deleted = New(3, "deleted", now.AddMinutes(1));
        deleted.DeletedAt = now;
        ContainerResource expired = New(4, "expired", now.AddMinutes(-1));
        List<CodeInterpreterExecutor.ContainerExecutionContext> result = CodeInterpreterExecutor.CollectActiveSessions([new ChatTurn { ContainerResources = [running, stopped, deleted, expired] }], now);
        Assert.Equal("running", Assert.Single(result).Label);
    }

    private static ContainerResource New(long id, string name, DateTime cleanupAt)
        => new() { Id = id, OwnerUserId = 1, RuntimeNodeId = 1, BackendResourceId = $"c{id}", Name = name, Image = "code-interpreter:latest", IsPermanent = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CleanupAt = cleanupAt };
}
