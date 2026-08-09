using Chats.BE.Controllers.Users.Mcps;
using Chats.BE.Services.Mcp;
using Chats.DB;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolNameMapperTests
{
    [Fact]
    public void Build_PreservesUniqueNamesAndDeterministicallyAliasesConflicts()
    {
        McpTool[] tools =
        [
            Tool(3, "unique"),
            Tool(2, "search"),
            Tool(1, "search"),
            Tool(4, "view_image"),
        ];

        IReadOnlyList<McpToolNameMapping> first = McpToolNameMapper.Build(tools, ["view_image"]);
        IReadOnlyList<McpToolNameMapping> second = McpToolNameMapper.Build(tools.Reverse(), ["view_image"]);

        Assert.Equal(
            ["mcp_1_search", "mcp_2_search", "unique", "mcp_4_view_image"],
            first.Select(x => x.ExposedName));
        Assert.Equal(first.Select(x => x.ExposedName), second.Select(x => x.ExposedName));
    }

    [Fact]
    public void Build_AvoidsCollisionWithAnUnchangedOriginalName()
    {
        McpTool[] tools =
        [
            Tool(1, "search"),
            Tool(2, "search"),
            Tool(3, "mcp_1_search"),
        ];

        IReadOnlyList<McpToolNameMapping> mappings = McpToolNameMapper.Build(tools, []);

        Assert.Equal(
            ["mcp_1_search_2", "mcp_2_search", "mcp_1_search"],
            mappings.Select(x => x.ExposedName));
        Assert.Equal(3, mappings.Select(x => x.ExposedName).Distinct(StringComparer.Ordinal).Count());
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
    public void FindLabelConflicts_OnlyMatchesTheTargetOwnerAndExcludesCurrentServer()
    {
        McpServer[] servers =
        [
            Server(1, ownerUserId: 7, label: "Shared"),
            Server(2, ownerUserId: 8, label: "Shared"),
            Server(3, ownerUserId: 7, label: "Other"),
        ];

        int[] conflicts = [.. McpController
            .FindLabelConflicts(servers.AsQueryable(), 7, "Shared")
            .Select(x => x.Id)];
        int[] excludingCurrent = [.. McpController
            .FindLabelConflicts(servers.AsQueryable(), 7, "Shared", excludedMcpServerId: 1)
            .Select(x => x.Id)];

        Assert.Equal([1], conflicts);
        Assert.Empty(excludingCurrent);
    }

    private static McpTool Tool(int serverId, string name)
        => new()
        {
            McpServerId = serverId,
            ToolName = name,
        };

    private static McpServer Server(
        int id,
        int ownerUserId,
        int? assignedUserId = null,
        string? label = null)
        => new()
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Label = label ?? $"server-{id}",
            Url = "https://example.com",
            UserMcps = assignedUserId.HasValue
                ? [new UserMcp { UserId = assignedUserId.Value, McpServerId = id }]
                : [],
        };
}
