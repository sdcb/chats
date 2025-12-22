using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.Web.ApiTest;

/// <summary>
/// 模型列表测试
/// </summary>
public class ModelsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ModelsTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetModels_ShouldReturnModelsList()
    {
        // 跳过测试如果配置中禁用
        if (!_fixture.Config.Tests.GetModels)
        {
            _output.WriteLine("GetModels test is disabled in configuration");
            return;
        }

        _output.WriteLine("Testing: Get Models");

        // Act
        HttpResponseMessage response = await _fixture.Client.GetAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/models");
        
        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();

        string content = await response.Content.ReadAsStringAsync();
        JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);
        
        Assert.NotNull(json);
        Assert.NotNull(json["data"]);

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Models: {json["data"]?.AsArray().Count ?? 0}");
        
        if (json["data"] is JsonArray models)
        {
            Assert.NotEmpty(models);
            foreach (JsonNode? model in models)
            {
                _output.WriteLine($"  - {model?["id"]} (owned by: {model?["owned_by"]})");
            }
        }
    }
}
