using Chats.BE.Services.CodeInterpreter;
using Chats.DB;
using Chats.DB.Enums;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterContextPrefixTests
{
    [Fact]
    public void BuildCodeInterpreterContextPrefix_NoFilesNoContainers_ReturnsNull()
        => Assert.Null(CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix([], DateTime.UtcNow));

    [Fact]
    public void BuildCodeInterpreterContextPrefix_OnlyActiveContainers_ReturnsContainer()
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource active = NewContainer(1, "s1", "c1", now.AddMinutes(10));
        ContainerResource deleted = NewContainer(2, "s2", "c2", now.AddMinutes(10));
        deleted.DeletedAt = now;
        ChatTurn turn = new() { ContainerResources = [active, deleted] };
        string? prefix = CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix([turn], now);
        Assert.NotNull(prefix);
        Assert.Contains("s1", prefix);
        Assert.DoesNotContain("s2", prefix);
    }

    [Fact]
    public void CollectCloudFiles_DuplicateNames_KeepsLast()
    {
        DBFile first = NewFile(1, 1);
        DBFile second = NewFile(2, 999);
        Step step = new() { ChatRoleId = (byte)DBChatRole.Assistant, CreatedAt = DateTime.UtcNow, StepContents = [StepContent.FromFile(first), StepContent.FromFile(second)] };
        Assert.Same(second, Assert.Single(CodeInterpreterExecutor.CollectCloudFiles([step])));
    }

    [Fact]
    public void CollectActiveSessions_DuplicateLabels_KeepsLast()
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource first = NewContainer(1, "s1", "c1", now.AddMinutes(10));
        ContainerResource second = NewContainer(2, "s1", "c2", now.AddMinutes(20));
        List<CodeInterpreterExecutor.ContainerExecutionContext> result = CodeInterpreterExecutor.CollectActiveSessions([new ChatTurn { ContainerResources = [first, second] }], now);
        Assert.Equal("c2", Assert.Single(result).ContainerId);
    }

    private static ContainerResource NewContainer(long id, string name, string backendId, DateTime cleanupAt)
        => new() { Id = id, OwnerUserId = 1, RuntimeNodeId = 1, BackendResourceId = backendId, Name = name, Image = "img", IsPermanent = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CleanupAt = cleanupAt };

    private static DBFile NewFile(int id, int size)
        => new() { Id = id, FileName = "dup.txt", StorageKey = $"k{id}", Size = size, MediaType = "text/plain", FileServiceId = 1, FileService = null!, ClientInfoId = 1, CreateUserId = 1, CreatedAt = DateTime.UtcNow, ClientInfo = null!, CreateUser = null! };
}
