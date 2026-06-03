using Chats.BE.Services.Models.Neutral;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.ChatServices;

public class NeutralConversionsTests
{
    [Fact]
    public void OpenAIParsing_ExtractsOnlyLeadingSystemPrefix_AndKeepsLaterSystemMessagesOrdered()
    {
        JsonArray messages = JsonNode.Parse("""
        [
          { "role": "system", "content": "outer system" },
          { "role": "developer", "content": "outer developer" },
          { "role": "user", "content": "first user" },
          { "role": "system", "content": "inner system" },
          { "role": "assistant", "content": "answer" },
          { "role": "developer", "content": "inner developer" }
        ]
        """)!.AsArray();

        string? systemPrompt = NeutralConversions.ExtractSystemPrompt(messages);
        IList<NeutralMessage> parsedMessages = NeutralConversions.ParseOpenAIMessages(messages);

        Assert.Equal("outer system\r\nouter developer", systemPrompt);
        Assert.Equal(
            [NeutralChatRole.User, NeutralChatRole.System, NeutralChatRole.Assistant, NeutralChatRole.System],
            parsedMessages.Select(x => x.Role).ToArray());
        Assert.Equal("first user", parsedMessages[0].GetTextContent());
        Assert.Equal("inner system", parsedMessages[1].GetTextContent());
        Assert.Equal("answer", parsedMessages[2].GetTextContent());
        Assert.Equal("inner developer", parsedMessages[3].GetTextContent());
    }
}
