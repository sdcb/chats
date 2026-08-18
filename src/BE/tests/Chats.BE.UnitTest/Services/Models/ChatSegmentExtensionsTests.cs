using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;

namespace Chats.BE.UnitTest.Services.Models;

public sealed class ChatSegmentExtensionsTests
{
    [Fact]
    public void AddMerged_ShouldKeepDistinctThinkingSignaturesSeparate()
    {
        const string firstSignature = "gAAAA-first-encrypted-reasoning";
        const string secondSignature = "gAAAA-second-encrypted-reasoning";
        List<ChatSegment> segments = [];

        segments.AddMerged(ChatSegment.FromThinkingSegment(firstSignature));
        segments.AddMerged(ChatSegment.FromThinkingSegment(secondSignature));

        Assert.Collection(
            segments,
            segment =>
            {
                ThinkChatSegment thinking = Assert.IsType<ThinkChatSegment>(segment);
                Assert.Equal(firstSignature, thinking.Signature);
            },
            segment =>
            {
                ThinkChatSegment thinking = Assert.IsType<ThinkChatSegment>(segment);
                Assert.Equal(secondSignature, thinking.Signature);
            });
    }

    [Fact]
    public void AddMerged_ShouldMergeThinkingTextWithFollowingSignature()
    {
        const string signature = "gAAAA-encrypted-reasoning";
        List<ChatSegment> segments = [];

        segments.AddMerged(ChatSegment.FromThink("reasoning"));
        segments.AddMerged(ChatSegment.FromThinkingSegment(signature));

        ThinkChatSegment thinking = Assert.IsType<ThinkChatSegment>(Assert.Single(segments));
        Assert.Equal("reasoning", thinking.Think);
        Assert.Equal(signature, thinking.Signature);
    }

    [Fact]
    public void AddMerged_ShouldStartNewThinkingSegmentAfterCompletedSignature()
    {
        const string signature = "gAAAA-encrypted-reasoning";
        List<ChatSegment> segments = [];

        segments.AddMerged(ChatSegment.FromThinkingSegment(signature));
        segments.AddMerged(ChatSegment.FromThink("next reasoning item"));

        Assert.Collection(
            segments,
            segment =>
            {
                ThinkChatSegment thinking = Assert.IsType<ThinkChatSegment>(segment);
                Assert.Equal(signature, thinking.Signature);
            },
            segment =>
            {
                ThinkChatSegment thinking = Assert.IsType<ThinkChatSegment>(segment);
                Assert.Equal("next reasoning item", thinking.Think);
                Assert.Null(thinking.Signature);
            });
    }

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
            IsCompleted = true,
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
                Assert.True(toolCall.IsCompleted);
            },
            segment => Assert.IsType<TextChatSegment>(segment));
    }
}
