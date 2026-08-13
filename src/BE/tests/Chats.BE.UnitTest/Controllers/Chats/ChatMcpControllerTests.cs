using Chats.BE.Controllers.Chats.Chats;
using Chats.DB;

namespace Chats.BE.UnitTest.Controllers.Chats;

public sealed class ChatMcpControllerTests
{
    [Fact]
    public void ApplyMcpState_EnableAddsMissingAssociationsAndRemovesDuplicates()
    {
        ChatConfig first = Config(1, 1, 1, 2);
        ChatConfig second = Config(2);

        ChatMcpController.ApplyMcpState([first, second], mcpServerId: 1, enabled: true);
        ChatMcpController.ApplyMcpState([first, second], mcpServerId: 1, enabled: true);

        Assert.Single(first.ChatConfigMcps, x => x.McpServerId == 1);
        Assert.Single(first.ChatConfigMcps, x => x.McpServerId == 2);
        Assert.Single(second.ChatConfigMcps, x => x.McpServerId == 1);
    }

    [Fact]
    public void ApplyMcpState_DisableRemovesAllTargetAssociations()
    {
        ChatConfig first = Config(1, 1, 2);
        ChatConfig second = Config(1, 1);

        ChatMcpController.ApplyMcpState([first, second], mcpServerId: 1, enabled: false);
        ChatMcpController.ApplyMcpState([first, second], mcpServerId: 1, enabled: false);

        Assert.DoesNotContain(first.ChatConfigMcps, x => x.McpServerId == 1);
        Assert.Single(first.ChatConfigMcps, x => x.McpServerId == 2);
        Assert.DoesNotContain(second.ChatConfigMcps, x => x.McpServerId == 1);
    }

    private static ChatConfig Config(params int[] mcpServerIds)
    {
        return new ChatConfig
        {
            ChatConfigMcps = [.. mcpServerIds.Select((mcpServerId, index) => new ChatConfigMcp
            {
                Id = index + 1,
                McpServerId = mcpServerId,
            })],
        };
    }
}
