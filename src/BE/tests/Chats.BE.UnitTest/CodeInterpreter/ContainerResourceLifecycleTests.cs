using Chats.BE.Services.CodeInterpreter;
using Chats.DB;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class ContainerResourceLifecycleTests
{
    [Fact]
    public void PermanentResource_HasNoCleanupDeadline()
    {
        ContainerResource resource = new() { OwnerUserId = 1, RuntimeNodeId = 1, IsPermanent = true, BackendResourceId = "docker-id", Name = "dev", Image = "code-interpreter:latest", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.Null(resource.CleanupAt);
        Assert.Null(resource.DeletedAt);
    }

    [Fact]
    public void TemporaryResource_CanBeTurnOwned()
    {
        ContainerResource resource = new() { OwnerUserId = 1, RuntimeNodeId = 1, IsPermanent = false, BackendResourceId = "docker-id", Name = "tmp", Image = "code-interpreter:latest", OwnerTurnId = 42, CleanupAt = DateTime.UtcNow.AddMinutes(30), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.Equal(42, resource.OwnerTurnId);
        Assert.False(resource.IsPermanent);
    }

    [Theory]
    [InlineData(true, false, false, 0, true)]
    [InlineData(true, true, false, 0, false)]
    [InlineData(true, false, true, 0, false)]
    [InlineData(false, false, false, 30, true)]
    [InlineData(false, true, false, 30, false)]
    [InlineData(false, false, true, 30, false)]
    [InlineData(false, false, false, -30, false)]
    [InlineData(false, false, false, 1, true)]
    [InlineData(false, false, false, 300, true)]
    [InlineData(true, false, false, -30, true)]
    [InlineData(true, false, false, 30, true)]
    [InlineData(false, true, true, 30, false)]
    [InlineData(true, true, true, 0, false)]
    [InlineData(false, false, true, -30, false)]
    public void ActiveResourceSelection_FollowsLifecycleFields(bool permanent, bool stopped, bool deleted, int cleanupSeconds, bool expected)
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource resource = new()
        {
            OwnerUserId = 1,
            RuntimeNodeId = 1,
            IsPermanent = permanent,
            BackendResourceId = "docker-id",
            Name = "resource",
            Image = "code-interpreter:latest",
            CreatedAt = now,
            UpdatedAt = now,
            CleanupAt = permanent ? null : now.AddSeconds(cleanupSeconds),
            StoppedAt = stopped ? now : null,
            DeletedAt = deleted ? now : null,
        };
        bool actual = CodeInterpreterExecutor.CollectActiveSessions([new ChatTurn { ContainerResources = [resource] }], now).Count == 1;
        Assert.Equal(expected, actual);
    }
}
