using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.Services.Models;

public class CcoWrapperTests
{
    [Fact]
    public void EnableSearch_ShouldDetectHostedWebSearchTool()
    {
        JsonObject json = JsonNode.Parse("""
        {
            "model": "gpt-5.5",
            "messages": [],
            "tools": [
                { "type": "web_search", "search_context_size": "low" },
                {
                    "type": "function",
                    "function": {
                        "name": "run_code",
                        "description": "Run code",
                        "parameters": { "type": "object" }
                    }
                }
            ]
        }
        """)!.AsObject();

        CcoWrapper wrapper = new(json);

        Assert.True(wrapper.EnableSearch);
        FunctionTool tool = Assert.Single(wrapper.Tools.OfType<FunctionTool>());
        Assert.Equal("run_code", tool.FunctionName);
    }
}
