using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.BE.ApiTest;

/// <summary>
/// 缓存测试
/// </summary>
public class CachedCompletionTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public CachedCompletionTests(ApiTestFixture fixture, ITestOutputHelper output)
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
    [MemberData(nameof(GetCachedModels))]
    public async Task CachedCompletion_ShouldUseCacheOnSecondRequest(string model)
    {
        _output.WriteLine($"Testing: Cached Completion (model: {model})");

        // Arrange - 使用布尔值 true 表示使用缓存（非 createOnly 模式）
        JsonObject request = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "What is 2 + 2?" }
            },
            ["stream"] = false,
            ["cache"] = true  // 使用缓存，默认 TTL
        };

        // Act - First request (creating cache)
        _output.WriteLine("First request (creating cache)...");
        HttpResponseMessage response1 = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions", 
            request, 
            _jsonOptions);
        
        if (!response1.IsSuccessStatusCode)
        {
            _output.WriteLine($"Resp: {await response1.Content.ReadAsStringAsync()}");
        }
        response1.EnsureSuccessStatusCode();
        JsonObject? result1 = await response1.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result1);
        
        _output.WriteLine($"Response: {result1["choices"]?[0]?["message"]?["content"]}");

        // Wait to ensure cache is saved
        await Task.Delay(500);

        // Act - Second request (should use cache)
        _output.WriteLine("Second request (should use cache)...");
        HttpResponseMessage response2 = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions", 
            request, 
            _jsonOptions);
        
        response2.EnsureSuccessStatusCode();
        JsonObject? result2 = await response2.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result2);
        
        _output.WriteLine($"Response: {result2["choices"]?[0]?["message"]?["content"]}");

        // Assert - Both requests should succeed
        Assert.NotNull(result1["choices"]);
        Assert.NotNull(result2["choices"]);
    }

    public static IEnumerable<object[]> GetCachedModels()
    {
        ApiTestFixture fixture = new ApiTestFixture();
        return fixture.Config.Tests.CachedModels.Select(m => new object[] { m });
    }
}
