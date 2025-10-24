using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.BE.ApiTest;

/// <summary>
/// 推理模型测试
/// </summary>
public class ReasoningModelTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReasoningModelTests(ApiTestFixture fixture, ITestOutputHelper output)
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
    [MemberData(nameof(GetReasoningModels))]
    public async Task ReasoningModel_ShouldReturnReasonedResponse(string model)
    {
        _output.WriteLine($"Testing: Reasoning Model (model: {model})");

        // Arrange
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "What is the capital of France? Think step by step." }
            },
            stream = false
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
        _output.WriteLine($"Model: {result["model"]}");
        _output.WriteLine($"Content: {result["choices"]?[0]?["message"]?["content"]}");
        _output.WriteLine($"Finish Reason: {result["choices"]?[0]?["finish_reason"]}");

        Assert.NotNull(result["choices"]);
        Assert.NotNull(result["choices"]?[0]?["message"]?["content"]);
    }

    public static IEnumerable<object[]> GetReasoningModels()
    {
        var fixture = new ApiTestFixture();
        return fixture.Config.Tests.ReasoningModels.Select(m => new object[] { m });
    }
}
