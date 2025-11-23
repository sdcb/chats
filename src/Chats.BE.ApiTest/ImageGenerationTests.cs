using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.BE.ApiTest;

/// <summary>
/// 图片生成测试
/// </summary>
public class ImageGenerationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions;

    public ImageGenerationTests(ApiTestFixture fixture, ITestOutputHelper output)
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
    [MemberData(nameof(GetImageGenerationModels))]
    public async Task ImageGeneration_NonStreaming_ShouldReturnImage(string model)
    {
        _output.WriteLine($"Testing: Image Generation (model: {model}, stream: false)");

        // Arrange
        // 注意: reasoning_effort 在图片生成中会被映射为 quality 参数
        // low -> quality: "low", medium -> quality: "medium", high -> quality: "high"
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "Generate an image of a cute cat" }
            },
            stream = false,
            reasoning_effort = "low"  // 使用 low 来加快生成速度
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
        _output.WriteLine($"Full Response Keys: {string.Join(", ", result.Select(kv => kv.Key))}");

        // 输出完整响应结构但不包含过长的 base64 数据
        string debugResult = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (debugResult.Length > 2000)
        {
            _output.WriteLine($"Response (first 2000 chars): {debugResult.Substring(0, 2000)}...");
        }
        else
        {
            _output.WriteLine($"Full Response: {debugResult}");
        }
        
        _output.WriteLine($"Model: {result["model"]}");

        // 获取 choices[0].message
        JsonNode? message = result["choices"]?[0]?["message"];
        _output.WriteLine($"Message is null: {message == null}");
        Assert.NotNull(message);
        
        // 图片在 segments 数组中，而不是 content
        JsonNode? segments = message["segments"];
        Assert.NotNull(segments);
        _output.WriteLine($"Segments type: {segments.GetType().Name}");
        
        if (segments is JsonArray segmentsArray)
        {
            _output.WriteLine($"Segments array length: {segmentsArray.Count}");
            bool hasImage = false;
            
            foreach (JsonNode? item in segmentsArray)
            {
                if (item is JsonObject obj)
                {
                    string? type = obj["$type"]?.GetValue<string>();
                    _output.WriteLine($"  Segment type: {type}");
                    
                    if (type == "base64")
                    {
                        hasImage = true;
                        string? contentType = obj["contentType"]?.GetValue<string>();
                        string? base64 = obj["base64"]?.GetValue<string>();
                        
                        Assert.NotNull(contentType);
                        Assert.NotNull(base64);
                        Assert.NotEmpty(base64);
                        Assert.StartsWith("image/", contentType);
                        
                        _output.WriteLine($"  Image Content-Type: {contentType}");
                        _output.WriteLine($"  Base64 length: {base64.Length} characters");
                    }
                }
            }
            
            Assert.True(hasImage, "Response should contain at least one base64 image in segments");
        }

        // 验证 finish_reason (可能为空)
        string? finishReason = result["choices"]?[0]?["finish_reason"]?.GetValue<string>();
        _output.WriteLine($"Finish Reason: {finishReason ?? "(null)"}");

        // 验证 usage - 非流式必须有 usage
        JsonNode? usage = result["usage"];
        Assert.NotNull(usage);
        
        int? promptTokens = usage["prompt_tokens"]?.GetValue<int>();
        int? completionTokens = usage["completion_tokens"]?.GetValue<int>();
        int? totalTokens = usage["total_tokens"]?.GetValue<int>();
        
        Assert.NotNull(promptTokens);
        Assert.NotNull(completionTokens);
        Assert.NotNull(totalTokens);
        Assert.True(promptTokens > 0, "Prompt tokens should be greater than 0");
        Assert.True(completionTokens > 0, "Completion tokens should be greater than 0");
        Assert.Equal(promptTokens + completionTokens, totalTokens);
        
        _output.WriteLine($"Usage: prompt_tokens={promptTokens}, completion_tokens={completionTokens}, total_tokens={totalTokens}");
    }

    [Theory]
    [MemberData(nameof(GetImageGenerationModels))]
    public async Task ImageGeneration_Streaming_ShouldReturnImage(string model)
    {
        _output.WriteLine($"Testing: Image Generation (model: {model}, stream: true)");

        // Arrange
        // 注意: reasoning_effort 在图片生成中会被映射为 quality 参数
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "Generate an image of a beautiful sunset" }
            },
            stream = true,
            reasoning_effort = "low"  // 使用 low 来加快生成速度
        };

        // Act
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_fixture.Config.OpenAICompatibleEndpoint}/chat/completions")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };
        
        HttpResponseMessage response = await _fixture.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        response.EnsureSuccessStatusCode();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine("Streaming response:");

        Stream sseStream = await response.Content.ReadAsStreamAsync();
        int chunkCount = 0;
        bool hasImageContent = false;
        string? finishReason = null;
        JsonNode? finalUsage = null;
        
        try
        {
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
                        
                        // 检查 finish_reason
                        JsonNode? currentFinishReason = chunk?["choices"]?[0]?["finish_reason"];
                        if (currentFinishReason != null)
                        {
                            finishReason = currentFinishReason.GetValue<string>();
                            _output.WriteLine($"Finish Reason: {finishReason}");
                        }
                        
                        // 检查 usage - 流式的最后一个 chunk 应该包含 usage
                        JsonNode? usage = chunk?["usage"];
                        if (usage != null)
                        {
                            finalUsage = usage;
                            int? pt = usage["prompt_tokens"]?.GetValue<int>();
                            int? ct = usage["completion_tokens"]?.GetValue<int>();
                            int? tt = usage["total_tokens"]?.GetValue<int>();
                            _output.WriteLine($"Usage: prompt_tokens={pt}, completion_tokens={ct}, total_tokens={tt}");
                        }
                        
                        // 检查是否有图片内容
                        if (delta?["image"] != null)
                        {
                            chunkCount++;
                            JsonNode? image = delta["image"];
                            
                            if (image is JsonObject imageObj)
                            {
                                string? type = imageObj["$type"]?.GetValue<string>();
                                string? contentType = imageObj["contentType"]?.GetValue<string>();
                                string? base64 = imageObj["base64"]?.GetValue<string>();
                                
                                // 支持 base64_preview (预览图) 和 base64 (最终图片)
                                if ((type == "base64" || type == "base64_preview") && !string.IsNullOrEmpty(base64))
                                {
                                    if (type == "base64")
                                    {
                                        hasImageContent = true;
                                        _output.WriteLine($"Chunk {chunkCount}: Received FINAL image (type={type}, contentType={contentType}, base64 length={base64.Length})");
                                    }
                                    else
                                    {
                                        _output.WriteLine($"Chunk {chunkCount}: Received preview image (type={type}, contentType={contentType}, base64 length={base64.Length})");
                                    }
                                }
                            }
                        }
                        
                        // 检查文本内容
                        if (delta?["content"] != null)
                        {
                            string? content = delta["content"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(content))
                            {
                                _output.WriteLine($"Chunk {chunkCount}: Text content: {content.Substring(0, Math.Min(50, content.Length))}...");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _output.WriteLine($"Failed to parse SSE data: {ex.Message}");
                        _output.WriteLine($"Data: {sse.Data.Substring(0, Math.Min(200, sse.Data.Length))}...");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Stream error: {ex.GetType().Name}: {ex.Message}");
            // 如果已经收到图片内容，即使连接提前结束也认为测试通过
            if (!hasImageContent)
            {
                throw;
            }
            _output.WriteLine("Stream ended prematurely but we already received image content, test passes");
        }

        Assert.True(hasImageContent, "Stream should contain at least one image chunk");
        Assert.NotNull(finalUsage);
        
        // 验证 usage 的值
        int? promptTokens = finalUsage["prompt_tokens"]?.GetValue<int>();
        int? completionTokens = finalUsage["completion_tokens"]?.GetValue<int>();
        int? totalTokens = finalUsage["total_tokens"]?.GetValue<int>();
        
        Assert.NotNull(promptTokens);
        Assert.NotNull(completionTokens);
        Assert.NotNull(totalTokens);
        Assert.True(promptTokens > 0, "Prompt tokens should be greater than 0");
        Assert.True(completionTokens > 0, "Completion tokens should be greater than 0");
        Assert.Equal(promptTokens + completionTokens, totalTokens);
        
        _output.WriteLine($"Total chunks received: {chunkCount}");
    }

    public static IEnumerable<object[]> GetImageGenerationModels()
    {
        ApiTestFixture fixture = new ApiTestFixture();
        return fixture.Config.Tests.ImageGenerationModels.Select(m => new object[] { m });
    }
}
