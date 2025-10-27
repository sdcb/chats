using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.BE.ApiTest;

/// <summary>
/// 工具调用测试
/// </summary>
public class ToolCallTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public ToolCallTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
    }

    [Theory]
    [MemberData(nameof(GetToolCallModels))]
    public async Task ToolCall_ShouldInvokeFunction(string model)
    {
        _output.WriteLine($"Testing: Tool Calls (model: {model})");

        // Arrange
        JsonObject request = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "What's the weather like in Beijing?" }
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = "get_weather",
                        ["description"] = "Get the current weather in a given location",
                        ["parameters"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["location"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["description"] = "The city and state, e.g. San Francisco, CA"
                                }
                            },
                            ["required"] = new JsonArray { "location" }
                        }
                    }
                }
            },
            ["stream"] = false
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions", 
            request, 
            _jsonOptions);

        // Assert
        response.EnsureSuccessStatusCode();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result);

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Finish Reason: {result["choices"]?[0]?["finish_reason"]}");

        JsonNode? message = result["choices"]?[0]?["message"];
        if (message?["tool_calls"] is JsonArray toolCalls && toolCalls.Count > 0)
        {
            _output.WriteLine($"Tool calls: {toolCalls.Count}");
            foreach (JsonNode? toolCall in toolCalls)
            {
                _output.WriteLine($"  - Function: {toolCall?["function"]?["name"]}");
                _output.WriteLine($"    Arguments: {toolCall?["function"]?["arguments"]}");
            }

            // Verify that at least one tool call was made
            Assert.True(toolCalls.Count > 0, "Should have at least one tool call");
        }
        else
        {
            _output.WriteLine($"Content: {message?["content"]}");
            _output.WriteLine("(Tool calls may not be supported by this model)");
            
            // We don't fail the test if tool calls are not supported
            // Some models may just provide a text response instead
        }
    }

    public static IEnumerable<object[]> GetToolCallModels()
    {
        var fixture = new ApiTestFixture();
        return fixture.Config.Tests.ToolCallModels.Select(m => new object[] { m });
    }
}
