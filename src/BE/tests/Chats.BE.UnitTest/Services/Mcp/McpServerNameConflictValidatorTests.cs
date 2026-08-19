using Chats.BE.Services.Mcp;
using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpServerNameConflictValidatorTests
{
    [Fact]
    public async Task FindConflictAsync_RejectsCaseInsensitiveDuplicateAcrossOwners()
    {
        await using ChatsDB db = CreateDb();
        db.McpServers.AddRange(
            Server(1, 10, "shared"),
            Server(2, 20, "SHARED"));
        await db.SaveChangesAsync();

        string? result = await McpServerNameConflictValidator.FindConflictAsync(
            db,
            [1, 2],
            CancellationToken.None);

        Assert.Contains("shared", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindConflictAsync_AllowsDistinctNamesAndRepeatedIds()
    {
        await using ChatsDB db = CreateDb();
        db.McpServers.AddRange(
            Server(1, 10, "first"),
            Server(2, 20, "second"));
        await db.SaveChangesAsync();

        string? result = await McpServerNameConflictValidator.FindConflictAsync(
            db,
            [1, 1, 2],
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HasRenameConflictAsync_DetectsConflictInExistingConfig()
    {
        await using ChatsDB db = CreateDb();
        db.McpServers.AddRange(
            Server(1, 10, "first"),
            Server(2, 20, "target"),
            Server(3, 30, "other"));
        db.ChatConfigMcps.AddRange(
            new ChatConfigMcp { ChatConfigId = 100, McpServerId = 1 },
            new ChatConfigMcp { ChatConfigId = 100, McpServerId = 2 },
            new ChatConfigMcp { ChatConfigId = 200, McpServerId = 1 },
            new ChatConfigMcp { ChatConfigId = 200, McpServerId = 3 });
        await db.SaveChangesAsync();

        Assert.True(await McpServerNameConflictValidator.HasRenameConflictAsync(
            db, 1, "TARGET", CancellationToken.None));
        Assert.False(await McpServerNameConflictValidator.HasRenameConflictAsync(
            db, 1, "unused", CancellationToken.None));
    }

    private static ChatsDB CreateDb()
    {
        DbContextOptions<ChatsDB> options = new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ChatsDB(options);
    }

    private static McpServer Server(int id, int ownerUserId, string name)
        => new()
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = name,
            Url = "https://example.com/mcp",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
}
