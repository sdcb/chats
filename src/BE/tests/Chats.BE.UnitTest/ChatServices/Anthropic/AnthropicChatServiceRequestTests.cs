using System.Reflection;
using System.Text.Json.Nodes;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices.Anthropic;

public class AnthropicChatServiceRequestTests
{
    [Fact]
    public void ConvertMessages_ToolMessageWithImage_NestsImageInsideToolResult()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "draw_chart", "{}")
            ),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "chart generated"),
                NeutralFileUrlContent.Create("https://example.com/chart.png")
            )
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        JsonObject userMessage = Assert.IsType<JsonObject>(result[1]);
        JsonArray content = Assert.IsType<JsonArray>(userMessage["content"]);
        JsonObject toolResult = Assert.IsType<JsonObject>(content[0]);
        Assert.Equal("tool_result", (string?)toolResult["type"]);

        JsonArray nestedContent = Assert.IsType<JsonArray>(toolResult["content"]);
        Assert.Equal("text", (string?)nestedContent[0]?["type"]);
        Assert.Equal("chart generated", (string?)nestedContent[0]?["text"]);
        Assert.Equal("image", (string?)nestedContent[1]?["type"]);
        Assert.Equal("https://example.com/chart.png", (string?)nestedContent[1]?["source"]?["url"]);
    }
}