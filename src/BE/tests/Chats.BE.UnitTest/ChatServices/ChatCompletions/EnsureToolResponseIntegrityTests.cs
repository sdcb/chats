using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class EnsureToolResponseIntegrityTests
{
    [Fact]
    public void NoToolCalls_ReturnsOriginal()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromUserText("hello"),
            NeutralMessage.FromAssistantText("hi"),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Same(messages, result);
    }

    [Fact]
    public void AllResponsesPresent_ReturnsOriginal()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "func", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "result")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Same(messages, result);
    }

    [Fact]
    public void MissingResponse_AddsEmptyResponse()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "func", "{}")),
            NeutralMessage.FromUserText("next"),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.NotSame(messages, result);
        Assert.Equal(3, result.Count);

        Assert.Equal(NeutralChatRole.Assistant, result[0].Role);
        Assert.Equal(NeutralChatRole.Tool, result[1].Role);
        Assert.Equal(NeutralChatRole.User, result[2].Role);

        var toolResponse = result[1].Contents.OfType<NeutralToolCallResponseContent>().First();
        Assert.Equal("call_1", toolResponse.ToolCallId);
        Assert.Equal("", toolResponse.Response);
    }

    [Fact]
    public void PartialMissing_AddsOnlyMissing()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "func1", "{}"),
                NeutralToolCallContent.Create("call_2", "func2", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "result1")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.NotSame(messages, result);
        Assert.Equal(3, result.Count);

        // Inserted missing response is at index 1 (right after assistant)
        Assert.Equal(NeutralChatRole.Tool, result[1].Role);
        var missingResponse = result[1].Contents.OfType<NeutralToolCallResponseContent>().First();
        Assert.Equal("call_2", missingResponse.ToolCallId);
        Assert.Equal("", missingResponse.Response);

        // Original response is at index 2
        Assert.Equal(NeutralChatRole.Tool, result[2].Role);
        var existingResponse = result[2].Contents.OfType<NeutralToolCallResponseContent>().First();
        Assert.Equal("call_1", existingResponse.ToolCallId);
        Assert.Equal("result1", existingResponse.Response);
    }

    [Fact]
    public void EmptyMessages_ReturnsOriginal()
    {
        IList<NeutralMessage> messages = [];
        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Same(messages, result);
    }

    [Fact]
    public void AllResponsesMissing_AddsAll()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "func1", "{}"),
                NeutralToolCallContent.Create("call_2", "func2", "{}")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.NotSame(messages, result);
        Assert.Equal(3, result.Count);

        var ids = result.Skip(1).SelectMany(m => m.Contents.OfType<NeutralToolCallResponseContent>())
            .Select(r => r.ToolCallId).ToList();
        Assert.Equal(["call_1", "call_2"], ids);
    }

    [Fact]
    public void MultipleAssistantMessages_EachValidated()
    {
        IList<NeutralMessage> messages = [
            // First turn: complete
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "func", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "ok")),
            // Second turn: missing response
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_2", "func", "{}")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Equal(4, result.Count);

        Assert.Equal(NeutralChatRole.Assistant, result[2].Role);
        Assert.Equal(NeutralChatRole.Tool, result[3].Role);
        var missing = result[3].Contents.OfType<NeutralToolCallResponseContent>().First();
        Assert.Equal("call_2", missing.ToolCallId);
    }

    [Fact]
    public void OrphanedToolResponse_BeforeAnyAssistant_Removed()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "result")),
            NeutralMessage.FromUserText("hello"),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Single(result);
        Assert.Equal(NeutralChatRole.User, result[0].Role);
    }

    [Fact]
    public void OrphanedToolResponse_AfterUser_Removed()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromUserText("hello"),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "result")),
            NeutralMessage.FromAssistantText("hi"),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Equal(2, result.Count);
        Assert.Equal(NeutralChatRole.User, result[0].Role);
        Assert.Equal(NeutralChatRole.Assistant, result[1].Role);
    }

    [Fact]
    public void OrphanedToolResponse_WrongCallId_Removed()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_A", "func", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_A", "ok")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_X", "orphaned")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Equal(2, result.Count);
        Assert.Equal(NeutralChatRole.Assistant, result[0].Role);
        Assert.Equal(NeutralChatRole.Tool, result[1].Role);
        var resp = result[1].Contents.OfType<NeutralToolCallResponseContent>().First();
        Assert.Equal("call_A", resp.ToolCallId);
    }

    [Fact]
    public void Mixed_MissingAndOrphaned_BothFixed()
    {
        IList<NeutralMessage> messages = [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_A", "func", "{}"),
                NeutralToolCallContent.Create("call_B", "func", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_A", "ok")),
            // call_B missing, call_X orphaned
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_X", "orphaned")),
        ];

        IList<NeutralMessage> result = NeutralConversions.EnsureToolResponseIntegrity(messages);
        Assert.Equal(3, result.Count);

        // assistant, then tool(call_B, ""), then tool(call_A)
        Assert.Equal(NeutralChatRole.Tool, result[1].Role);
        Assert.Equal(NeutralChatRole.Tool, result[2].Role);
        var ids = result.Skip(1).SelectMany(m => m.Contents.OfType<NeutralToolCallResponseContent>())
            .Select(r => r.ToolCallId).ToList();
        Assert.Contains("call_A", ids);
        Assert.Contains("call_B", ids);
        Assert.DoesNotContain("call_X", ids);
    }
}
