using Chats.BE.Services.Mcp;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpServerInstructionsBuilderTests
{
    [Fact]
    public void MergeSystemMessage_DisambiguatesDuplicateLabelsWithStableIds()
    {
        McpServer[] servers =
        [
            Server(2, "Shared", "second"),
            Server(1, "Shared", "first"),
            Server(3, "Unique", "third"),
        ];

        NeutralSystemMessage? result = McpServerInstructionsBuilder.MergeSystemMessage(null, servers);

        string text = Assert.Single(result!.Contents).Text;
        Assert.Equal(
            "### MCP: Shared (#1)\nfirst\n\n### MCP: Shared (#2)\nsecond\n\n### MCP: Unique\nthird",
            text);
    }

    private static McpServer Server(int id, string label, string instructions)
        => new()
        {
            Id = id,
            Label = label,
            Url = "https://example.com",
            ServerInstructions = instructions,
        };
}
