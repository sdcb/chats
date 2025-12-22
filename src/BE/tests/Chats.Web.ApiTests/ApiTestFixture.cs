using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace Chats.Web.ApiTest;

/// <summary>
/// 测试配置类
/// </summary>
public class TestConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string OpenAICompatibleEndpoint { get; set; } = string.Empty;
    public TestsConfig Tests { get; set; } = new();
}

public class TestsConfig
{
    public string[] NonStreamingModels { get; set; } = Array.Empty<string>();
    public string[] StreamingModels { get; set; } = Array.Empty<string>();
    public string[] ReasoningModels { get; set; } = Array.Empty<string>();
    public string[] CachedModels { get; set; } = Array.Empty<string>();
    public string[] ToolCallModels { get; set; } = Array.Empty<string>();
    public string[] ImageGenerationModels { get; set; } = Array.Empty<string>();
    public bool GetModels { get; set; } = true;
}

/// <summary>
/// 测试固件 - 管理 HttpClient 和配置
/// </summary>
public class ApiTestFixture : IDisposable
{
    public HttpClient Client { get; }
    public TestConfiguration Config { get; }

    public ApiTestFixture()
    {
        // 读取配置
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<ApiTestFixture>(optional: true)
            .Build();

        Config = new TestConfiguration();
        configuration.Bind(Config);

        // 验证配置
        if (string.IsNullOrEmpty(Config.ApiKey))
            throw new InvalidOperationException("ApiKey not found in appsettings.json");
        if (string.IsNullOrEmpty(Config.OpenAICompatibleEndpoint))
            throw new InvalidOperationException("OpenAICompatibleEndpoint not found in appsettings.json");

        // 配置 HttpClient
        Client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
    }

    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
