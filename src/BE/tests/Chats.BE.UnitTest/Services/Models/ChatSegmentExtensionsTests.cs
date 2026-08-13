using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;

namespace Chats.BE.UnitTest.Services.Models;

public sealed class ChatSegmentExtensionsTests
{
    [Fact]
    public void AddMerged_ShouldMergeToolCallFragmentsAcrossTextSegments()
    {
        List<ChatSegment> segments = [];

        segments.AddMerged(new ToolCallSegment
        {
            Index = 0,
            Id = "call_a8326c11b3744411bc42065a",
            Name = "create_docker_session",
            Arguments = "",
        });
        segments.AddMerged(ChatSegment.FromText("<tool_call>\n<function=create_docker_session>\n"));
        segments.AddMerged(new ToolCallSegment
        {
            Index = 0,
            Arguments = "{\"memoryBytes\": ",
        });
        segments.AddMerged(ChatSegment.FromText("536870912</parameter>\n</function>\n</tool_call>"));
        segments.AddMerged(new ToolCallSegment
        {
            Index = 0,
            Arguments = "536870912}",
        });

        Assert.Collection(
            segments,
            segment =>
            {
                ToolCallSegment toolCall = Assert.IsType<ToolCallSegment>(segment);
                Assert.Equal(0, toolCall.Index);
                Assert.Equal("call_a8326c11b3744411bc42065a", toolCall.Id);
                Assert.Equal("create_docker_session", toolCall.Name);
                Assert.Equal("{\"memoryBytes\": 536870912}", toolCall.Arguments);
            },
            segment => Assert.IsType<TextChatSegment>(segment));
    }
}
