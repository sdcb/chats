using Chats.BE.Controllers.Users.Mcps;
using Chats.BE.Controllers.Users.Mcps.Dtos;
using Chats.DB;
using ModelContextProtocol.Protocol;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolMetadataMappingTests
{
    [Fact]
    public void MapTool_UsesProtocolTitleBeforeAnnotationTitleAndCopiesHints()
    {
        Tool tool = new()
        {
            Name = "search",
            Title = "Protocol title",
            Description = "Find things",
            Annotations = new ToolAnnotations
            {
                Title = "Annotation title",
                DestructiveHint = true,
                IdempotentHint = true,
                OpenWorldHint = true,
                ReadOnlyHint = true,
            },
        };

        McpToolBasicInfo mapped = McpController.MapTool(tool);

        Assert.Equal("search", mapped.Name);
        Assert.Equal("Protocol title", mapped.Title);
        Assert.Equal("Find things", mapped.Description);
        Assert.Contains("\"type\":\"object\"", mapped.Parameters);
        Assert.True(mapped.Destructive);
        Assert.True(mapped.Idempotent);
        Assert.True(mapped.OpenWorld);
        Assert.True(mapped.ReadOnly);
    }

    [Fact]
    public void MapTool_FallsBackToAnnotationTitleAndMapsNullHintsToFalse()
    {
        Tool tool = new()
        {
            Name = "search",
            Annotations = new ToolAnnotations { Title = "Annotation title" },
        };

        McpToolBasicInfo mapped = McpController.MapTool(tool);

        Assert.Equal("Annotation title", mapped.Title);
        Assert.False(mapped.Destructive);
        Assert.False(mapped.Idempotent);
        Assert.False(mapped.OpenWorld);
        Assert.False(mapped.ReadOnly);
    }

    [Fact]
    public void ToDb_CopiesAllEditableMetadata()
    {
        McpToolBasicInfo source = new()
        {
            Name = "search",
            Title = "Search",
            Description = "Find things",
            Parameters = "{\"type\":\"object\"}",
            Destructive = true,
            Idempotent = true,
            OpenWorld = true,
            ReadOnly = true,
        };

        McpTool mapped = source.ToDB();

        Assert.Equal(source.Name, mapped.ToolName);
        Assert.Equal(source.Title, mapped.Title);
        Assert.Equal(source.Description, mapped.Description);
        Assert.Equal(source.Parameters, mapped.Parameters);
        Assert.Equal(source.Destructive, mapped.Destructive);
        Assert.Equal(source.Idempotent, mapped.Idempotent);
        Assert.Equal(source.OpenWorld, mapped.OpenWorld);
        Assert.Equal(source.ReadOnly, mapped.ReadOnly);
    }
}
