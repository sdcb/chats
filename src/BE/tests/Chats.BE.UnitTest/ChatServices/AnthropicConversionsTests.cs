using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.ChatServices;

public class AnthropicConversionsTests
{
    [Fact]
    public void ParseAnthropicMessages_MultipleToolResultsInSingleUserMessage_SplitsIntoDistinctToolMessages()
    {
        // Arrange
        JsonNode? messagesNode = JsonNode.Parse("""
        [
          {
            "role": "assistant",
            "content": [
              {
                "type": "tool_use",
                "id": "call_1",
                "name": "Glob",
                "input": {
                  "pattern": "**/*.cs"
                }
              },
              {
                "type": "tool_use",
                "id": "call_2",
                "name": "Glob",
                "input": {
                  "pattern": "**/*.{ts,tsx}"
                }
              }
            ]
          },
          {
            "role": "user",
            "content": [
              {
                "type": "tool_result",
                "tool_use_id": "call_1",
                "content": "csharp files"
              },
              {
                "type": "tool_result",
                "tool_use_id": "call_2",
                "content": "typescript files"
              }
            ]
          }
        ]
        """);

        // Act
        IList<NeutralMessage> messages = AnthropicConversions.ParseAnthropicMessages(messagesNode);

        // Assert
        Assert.Collection(messages,
            assistant =>
            {
                Assert.Equal(NeutralChatRole.Assistant, assistant.Role);
                Assert.Equal(2, assistant.Contents.OfType<NeutralToolCallContent>().Count());
            },
            tool1 =>
            {
                Assert.Equal(NeutralChatRole.Tool, tool1.Role);
                NeutralToolCallResponseContent response = Assert.Single(tool1.Contents.OfType<NeutralToolCallResponseContent>());
                Assert.Equal("call_1", response.ToolCallId);
                Assert.Equal("csharp files", response.Response);
            },
            tool2 =>
            {
                Assert.Equal(NeutralChatRole.Tool, tool2.Role);
                NeutralToolCallResponseContent response = Assert.Single(tool2.Contents.OfType<NeutralToolCallResponseContent>());
                Assert.Equal("call_2", response.ToolCallId);
                Assert.Equal("typescript files", response.Response);
            });
    }
}