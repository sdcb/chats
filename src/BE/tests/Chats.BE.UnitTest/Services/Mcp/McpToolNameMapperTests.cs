using Chats.BE.Controllers.Users.Mcps;
using Chats.BE.Services.Mcp;
using Chats.DB;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolNameMapperTests
{
    [Fact]
    public void Build_UsesFixedNamespaceAndStableOrdering()
    {
        McpTool[] tools =
        [
            Tool(3, "gamma", "unique"),
            Tool(2, "beta", "search"),
            Tool(1, "alpha", "search"),
            Tool(4, "delta", "view_image"),
        ];

        IReadOnlyList<McpToolNameMapping> first = McpToolNameMapper.Build(tools, ["view_image"]);
        IReadOnlyList<McpToolNameMapping> second = McpToolNameMapper.Build(tools.Reverse(), ["view_image"]);

        Assert.Equal(
            ["mcp__alpha__search", "mcp__beta__search", "mcp__delta__view_image", "mcp__gamma__unique"],
            first.Select(x => x.ExposedName));
        Assert.Equal(first.Select(x => x.ExposedName), second.Select(x => x.ExposedName));
        Assert.Equal(["search", "search", "view_image", "unique"], first.Select(x => x.Tool.ToolName));
    }

    [Fact]
    public void Build_RejectsReservedProtocolName()
    {
        McpTool tool = Tool(1, "alpha", "search");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => McpToolNameMapper.Build([tool], ["mcp__alpha__search"]));

        Assert.Contains("duplicated or reserved", ex.Message);
    }

    [Theory]
    [InlineData("bad.name", "search")]
    [InlineData("alpha", "bad.name")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcdefghij", "tool_name_that_is_too_long")]
    public void Build_RejectsInvalidOrOverlongProtocolName(string serverName, string toolName)
    {
        McpTool tool = Tool(1, serverName, toolName);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => McpToolNameMapper.Build([tool], []));

        Assert.Contains(toolName, ex.Message);
    }

    [Fact]
    public void ApplyManagementScope_AdminMineIncludesOwnedAndAssigned()
    {
        McpServer[] servers =
        [
            Server(1, ownerUserId: 7),
            Server(2, ownerUserId: 8, assignedUserId: 7),
            Server(3, ownerUserId: 8),
        ];

        int[] mine = [.. McpController
            .ApplyManagementScope(servers.AsQueryable(), 7, isAdmin: true, mineOnly: true)
            .Select(x => x.Id)];
        int[] all = [.. McpController
            .ApplyManagementScope(servers.AsQueryable(), 7, isAdmin: true, mineOnly: false)
            .Select(x => x.Id)];
        int[] nonAdmin = [.. McpController
            .ApplyManagementScope(servers.AsQueryable(), 7, isAdmin: false, mineOnly: false)
            .Select(x => x.Id)];

        Assert.Equal([1, 2], mine);
        Assert.Equal([1, 2, 3], all);
        Assert.Equal([1, 2], nonAdmin);
    }

    [Fact]
    public void FindNameConflicts_IsCaseInsensitiveWithinOwnerAndExcludesCurrentServer()
    {
        McpServer[] servers =
        [
            Server(1, ownerUserId: 7, name: "Shared"),
            Server(2, ownerUserId: 8, name: "Shared"),
            Server(3, ownerUserId: 7, name: "Other"),
        ];

        int[] conflicts = [.. McpController
            .FindNameConflicts(servers.AsQueryable(), 7, "shared")
            .Select(x => x.Id)];
        int[] excludingCurrent = [.. McpController
            .FindNameConflicts(servers.AsQueryable(), 7, "SHARED", excludedMcpServerId: 1)
            .Select(x => x.Id)];

        Assert.Equal([1], conflicts);
        Assert.Empty(excludingCurrent);
    }

    private static McpTool Tool(int serverId, string serverName, string toolName)
    {
        McpServer server = Server(serverId, ownerUserId: serverId, name: serverName);
        return new()
        {
            McpServerId = serverId,
            McpServer = server,
            ToolName = toolName,
        };
    }

    private static McpServer Server(
        int id,
        int ownerUserId,
        int? assignedUserId = null,
        string? name = null)
        => new()
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = name ?? $"server-{id}",
            Url = "https://example.com",
            UserMcps = assignedUserId.HasValue
                ? [new UserMcp { UserId = assignedUserId.Value, McpServerId = id }]
                : [],
        };
}
