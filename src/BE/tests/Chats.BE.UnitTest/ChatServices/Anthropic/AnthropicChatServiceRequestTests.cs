using System.Reflection;
using System.Text.Json.Nodes;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices.Anthropic;

public class AnthropicChatServiceRequestTests
{
    [Fact]
    public void ConvertMessages_ApiSource_PreservesThinkingBlocks()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("thinking text", "thinking-signature"),
                NeutralTextContent.Create("answer")
            )
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        JsonObject assistantMessage = Assert.IsType<JsonObject>(result[0]);
        JsonArray content = Assert.IsType<JsonArray>(assistantMessage["content"]);
        JsonObject thinking = Assert.IsType<JsonObject>(content[0]);

        Assert.Equal("thinking", (string?)thinking["type"]);
        Assert.Equal("thinking text", (string?)thinking["thinking"]);
        Assert.Equal("thinking-signature", (string?)thinking["signature"]);
    }

    [Fact]
    public void ConvertMessages_SystemMessage_SerializesInPlace()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromUserText("first"),
            NeutralMessage.FromSystemText("inner system"),
            NeutralMessage.FromAssistantText("answer")
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        Assert.Equal(["user", "system", "assistant"], result.Select(x => x!["role"]!.GetValue<string>()).ToArray());

        JsonObject systemMessage = Assert.IsType<JsonObject>(result[1]);
        JsonArray content = Assert.IsType<JsonArray>(systemMessage["content"]);
        Assert.Equal("text", (string?)content[0]?["type"]);
        Assert.Equal("inner system", (string?)content[0]?["text"]);
    }

    [Fact]
    public void ConvertMessages_AssistantToolCallWithEmptyParameters_UsesEmptyObjectInput()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "create_docker_session", "")
            )
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        JsonObject assistantMessage = Assert.IsType<JsonObject>(result[0]);
        JsonArray content = Assert.IsType<JsonArray>(assistantMessage["content"]);
        JsonObject toolUse = Assert.IsType<JsonObject>(content[0]);

        Assert.Equal("tool_use", (string?)toolUse["type"]);
        Assert.Equal("call_1", (string?)toolUse["id"]);
        Assert.Equal("create_docker_session", (string?)toolUse["name"]);
        Assert.IsType<JsonObject>(toolUse["input"]);
        Assert.Empty(Assert.IsType<JsonObject>(toolUse["input"]));
    }

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

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
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

    [Fact]
    public void ConvertMessages_ParallelToolResults_MergesIntoSingleFollowingUserMessage()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "create_docker_session", "{}"),
                NeutralToolCallContent.Create("call_2", "download_chat_files", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "sessionId: abc")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_2", "Session not found", isSuccess: false)),
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        Assert.Equal(2, result.Count);
        JsonObject userMessage = Assert.IsType<JsonObject>(result[1]);
        Assert.Equal("user", (string?)userMessage["role"]);
        JsonArray content = Assert.IsType<JsonArray>(userMessage["content"]);
        Assert.Equal(["call_1", "call_2"], content.Select(x => (string?)x!["tool_use_id"]));
        Assert.Null(content[0]?["is_error"]);
        Assert.Equal(true, (bool?)content[1]?["is_error"]);
    }

    [Fact]
    public void ConvertMessages_ParallelToolResultsWithImages_PreservesGroupOrderAndAttachments()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "view_image", "{}"),
                NeutralToolCallContent.Create("call_2", "view_image", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "first image"),
                NeutralFileUrlContent.Create("https://example.com/first.png")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_2", "second image"),
                NeutralFileUrlContent.Create("https://example.com/second.png")),
        ];

        JsonArray result = (JsonArray?)method.Invoke(null, [messages, true, UsageSource.Api, false])
            ?? throw new InvalidOperationException("ConvertMessages returned null.");

        JsonArray content = Assert.IsType<JsonArray>(result[1]!["content"]);
        Assert.Equal(2, content.Count);
        Assert.Equal("call_1", (string?)content[0]?["tool_use_id"]);
        Assert.Equal("https://example.com/first.png", (string?)content[0]?["content"]?[1]?["source"]?["url"]);
        Assert.Equal("call_2", (string?)content[1]?["tool_use_id"]);
        Assert.Equal("https://example.com/second.png", (string?)content[1]?["content"]?[1]?["source"]?["url"]);
    }
}
