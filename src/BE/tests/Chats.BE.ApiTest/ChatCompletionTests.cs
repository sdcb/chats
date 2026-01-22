using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.BE.ApiTest;

/// <summary>
/// 聊天完成测试
/// </summary>
public class ChatCompletionTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChatCompletionTests(ApiTestFixture fixture, ITestOutputHelper output)
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
    [MemberData(nameof(GetNonStreamingModels))]
    public async Task ChatCompletion_NonStreaming_ShouldReturnResponse(string model)
    {
        _output.WriteLine($"Testing: Chat Completion (model: {model}, stream: false)");

        // Arrange
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "1 + 1 = ?" }
            },
            stream = false,
            temperature = 1.0
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions", 
            request, 
            _jsonOptions);

        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result);

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Model: {result["model"]}");
        _output.WriteLine($"Content: {result["choices"]?[0]?["message"]?["content"]}");
        _output.WriteLine($"Finish Reason: {result["choices"]?[0]?["finish_reason"]}");
        _output.WriteLine($"Usage: prompt_tokens={result["usage"]?["prompt_tokens"]}, completion_tokens={result["usage"]?["completion_tokens"]}");

        Assert.NotNull(result["choices"]);
        Assert.NotNull(result["choices"]?[0]?["message"]?["content"]);
    }

    [Theory]
    [MemberData(nameof(GetStreamingModels))]
    public async Task ChatCompletion_Streaming_ShouldReturnStreamResponse(string model)
    {
        _output.WriteLine($"Testing: Chat Completion (model: {model}, stream: true)");

        // Arrange
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "1 + 1 = ?" }
            },
            stream = true,
            temperature = 1.0
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions", 
            request, 
            _jsonOptions);

        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine("Streaming response:");

        Stream sseStream = await response.Content.ReadAsStreamAsync();
        int chunkCount = 0;
        System.Text.StringBuilder contentBuilder = new System.Text.StringBuilder();
        
        await foreach (SseItem<string> sse in SseParser.Create(sseStream).EnumerateAsync())
        {
            if (sse.EventType == "done" || sse.Data == "[DONE]")
            {
                _output.WriteLine("\n[DONE]");
                break;
            }
            
            if (!string.IsNullOrEmpty(sse.Data))
            {
                try
                {
                    JsonObject? chunk = JsonSerializer.Deserialize<JsonObject>(sse.Data);
                    JsonNode? delta = chunk?["choices"]?[0]?["delta"];
                    string? content = delta?["content"]?.GetValue<string>();
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        contentBuilder.Append(content);
                        chunkCount++;
                    }
                }
                catch { }
            }
        }
        
        _output.WriteLine($"Content: {contentBuilder}");
        _output.WriteLine($"Received {chunkCount} content chunks");
        
        Assert.True(chunkCount > 0, "Should receive at least one content chunk");
    }

    public static IEnumerable<object[]> GetNonStreamingModels()
    {
        ApiTestFixture fixture = new ApiTestFixture();
        return fixture.Config.Tests.NonStreamingModels.Select(m => new object[] { m });
    }

    public static IEnumerable<object[]> GetStreamingModels()
    {
        ApiTestFixture fixture = new ApiTestFixture();
        return fixture.Config.Tests.StreamingModels.Select(m => new object[] { m });
    }
}
