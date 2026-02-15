using Chats.BE.Services.CodeInterpreter;
using Chats.DockerInterface.Models;
using Chats.DB;
using Chats.DB.Enums;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterContextPrefixTests
{
    [Fact]
    public void BuildCodeInterpreterContextPrefix_NoFilesNoSessions_ShouldReturnNull()
    {
        string? prefix = CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix(
            messageTurns: [],
            utcNow: DateTime.UtcNow);

        Assert.Null(prefix);
    }

    [Fact]
    public void BuildCodeInterpreterContextPrefix_OnlyActiveSessions_ShouldReturnNonNull()
    {
        DateTime now = DateTime.UtcNow;

        ChatDockerSession active = new()
        {
            Id = 1,
            Label = "s1",
            ContainerId = "c1",
            Image = "img1",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

        ChatDockerSession terminated = new()
        {
            Id = 2,
            Label = "s2",
            ContainerId = "c2",
            Image = "img2",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.AddMinutes(10),
            TerminatedAt = now,
        };

        ChatTurn t = new() { ChatDockerSessions = [active, terminated] };

        string? prefix = CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix(
            messageTurns: [t],
            utcNow: now);

        Assert.NotNull(prefix);
        Assert.Contains("[Active Docker Sessions]", prefix);
        Assert.Contains("sessionId: s1", prefix);
        Assert.DoesNotContain("sessionId: s2", prefix);
    }

    [Fact]
    public void CollectCloudFiles_DuplicateNames_ShouldKeepLast()
    {
        DateTime now = DateTime.UtcNow;

        DBFile first = new()
        {
            Id = 1,
            FileName = "dup.txt",
            StorageKey = "k1",
            Size = 1,
            MediaType = "text/plain",
            FileServiceId = 1,
            FileService = null!,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = now,
            ClientInfo = null!,
            CreateUser = null!,
        };

        DBFile second = new()
        {
            Id = 2,
            FileName = "dup.txt",
            StorageKey = "k2",
            Size = 999,
            MediaType = "text/plain",
            FileServiceId = 1,
            FileService = null!,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = now,
            ClientInfo = null!,
            CreateUser = null!,
        };

        Step s1 = new()
        {
            ChatRoleId = (byte)DBChatRole.Assistant,
            CreatedAt = now,
            StepContents = [StepContent.FromFile(first), StepContent.FromFile(second)],
        };

        List<DBFile> files = CodeInterpreterExecutor.CollectCloudFiles([s1]);

        DBFile only = Assert.Single(files);
        Assert.Same(second, only);
        Assert.Equal(999, only.Size);
    }

    [Fact]
    public void CollectActiveSessions_DuplicateLabels_ShouldKeepLastActive()
    {
        DateTime now = DateTime.UtcNow;

        ChatDockerSession first = new()
        {
            Id = 1,
            Label = "s1",
            ContainerId = "c1",
            Image = "img1",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.AddMinutes(10),
        };

        ChatDockerSession second = new()
        {
            Id = 2,
            Label = "s1",
            ContainerId = "c2",
            Image = "img2",
            ShellPrefix = "/bin/sh,-lc",
            NetworkMode = (byte)NetworkMode.None,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.AddMinutes(20),
        };

        ChatTurn t = new() { ChatDockerSessions = [first, second] };

        List<ChatDockerSession> sessions = CodeInterpreterExecutor.CollectActiveSessions([t], now);

        ChatDockerSession only = Assert.Single(sessions);
        Assert.Same(second, only);
        Assert.Equal("img2", only.Image);
    }

    [Fact]
    public void BuildCodeInterpreterContextPrefix_OnlyFiles_ShouldReturnNonNull()
    {
        DateTime now = DateTime.UtcNow;

        DBFile f = new()
        {
            Id = 1,
            FileName = "a.txt",
            StorageKey = "a",
            Size = 1,
            MediaType = "text/plain",
            FileServiceId = 1,
            FileService = null!,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = now,
            ClientInfo = null!,
            CreateUser = null!,
        };

        Step step = new()
        {
            ChatRoleId = (byte)DBChatRole.Assistant,
            CreatedAt = now,
            StepContents = [StepContent.FromFile(f)],
        };

        ChatTurn t = new() { Steps = [step] };

        string? prefix = CodeInterpreterExecutor.BuildCodeInterpreterContextPrefix(
            messageTurns: [t],
            utcNow: now);

        Assert.NotNull(prefix);
        Assert.Contains("[Cloud Files Available]", prefix);
        Assert.Contains("a.txt", prefix);
    }
}
