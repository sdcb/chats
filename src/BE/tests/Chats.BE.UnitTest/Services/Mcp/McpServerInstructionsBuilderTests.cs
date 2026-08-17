using Chats.BE.Services.Mcp;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpServerInstructionsBuilderTests
{
    [Fact]
    public void MergeSystemMessage_UsesDisplayNameAndSortsByProtocolName()
    {
        McpServer[] servers =
        [
            Server(2, "zeta", "Shared", "second"),
            Server(1, "alpha", "Shared", "first"),
            Server(3, "middle", null, "third"),
        ];

        NeutralSystemMessage? result = McpServerInstructionsBuilder.MergeSystemMessage(null, servers);

        string text = Assert.Single(result!.Contents).Text;
        Assert.Equal(
            "### MCP: Shared (alpha)\nfirst\n\n### MCP: middle\nthird\n\n### MCP: Shared (zeta)\nsecond",
            text);
    }

    private static McpServer Server(int id, string name, string? displayName, string instructions)
        => new()
        {
            Id = id,
            Name = name,
            DisplayName = displayName,
            Url = "https://example.com",
            ServerInstructions = instructions,
        };
}
