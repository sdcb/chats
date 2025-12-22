using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Chats.Web.ApiTest;

/// <summary>
/// 图片生成测试 - 使用 OpenAI 兼容的 v1/images API
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

    /// <summary>
    /// 测试非流式图片生成
    /// 验证点：
    /// - 返回状态码 200
    /// - 响应包含 created, data, usage 字段
    /// - data 数组中包含 b64_json 图片数据
    /// - usage 包含 input_tokens, output_tokens, total_tokens
    /// </summary>
    [Theory]
    [MemberData(nameof(GetImageGenerationModels))]
    public async Task ImageGeneration_NonStreaming_ShouldReturnImage(string model)
    {
        _output.WriteLine($"Testing: v1/images/generations (model: {model}, stream: false)");

        // Arrange
        var request = new
        {
            prompt = "Generate an image of a cute cat",
            model,
            n = 1,
            quality = "low",  // 使用 low 来加快生成速度
            size = "1024x1024"
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/images/generations",
            request,
            _jsonOptions);

        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result);

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response Keys: {string.Join(", ", result.Select(kv => kv.Key))}");

        // 输出完整响应结构但不包含过长的 base64 数据
        string debugResult = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (debugResult.Length > 2000)
        {
            _output.WriteLine($"Response (first 2000 chars): {debugResult[..2000]}...");
        }
        else
        {
            _output.WriteLine($"Full Response: {debugResult}");
        }

        // 验证 created 字段
        long? created = result["created"]?.GetValue<long>();
        Assert.NotNull(created);
        Assert.True(created > 0, "created should be a valid Unix timestamp");
        _output.WriteLine($"Created: {created}");

        // 验证 data 数组
        JsonNode? data = result["data"];
        Assert.NotNull(data);
        Assert.True(data is JsonArray, "data should be an array");

        JsonArray dataArray = (JsonArray)data;
        Assert.True(dataArray.Count > 0, "data array should contain at least one image");
        _output.WriteLine($"Data array length: {dataArray.Count}");

        // 验证每个图片数据
        foreach (JsonNode? item in dataArray)
        {
            Assert.NotNull(item);
            string? b64Json = item["b64_json"]?.GetValue<string>();
            Assert.NotNull(b64Json);
            Assert.NotEmpty(b64Json);
            _output.WriteLine($"Image b64_json length: {b64Json.Length} characters");

            // 可选：验证是否为有效的 base64
            try
            {
                byte[] imageBytes = Convert.FromBase64String(b64Json);
                Assert.True(imageBytes.Length > 0, "Decoded image should have content");
                _output.WriteLine($"Decoded image size: {imageBytes.Length} bytes");
            }
            catch (FormatException)
            {
                Assert.Fail("b64_json should be valid base64 encoded data");
            }
        }

        // 验证 usage
        JsonNode? usage = result["usage"];
        Assert.NotNull(usage);

        int? inputTokens = usage["input_tokens"]?.GetValue<int>();
        int? outputTokens = usage["output_tokens"]?.GetValue<int>();
        int? totalTokens = usage["total_tokens"]?.GetValue<int>();

        Assert.NotNull(inputTokens);
        Assert.NotNull(outputTokens);
        Assert.NotNull(totalTokens);
        Assert.True(inputTokens > 0, "input_tokens should be greater than 0");
        Assert.True(outputTokens > 0, "output_tokens should be greater than 0");
        Assert.Equal(inputTokens + outputTokens, totalTokens);

        _output.WriteLine($"Usage: input_tokens={inputTokens}, output_tokens={outputTokens}, total_tokens={totalTokens}");

        // 验证可选字段
        string? outputFormat = result["output_format"]?.GetValue<string>();
        string? quality = result["quality"]?.GetValue<string>();
        string? size = result["size"]?.GetValue<string>();
        _output.WriteLine($"Optional fields: output_format={outputFormat}, quality={quality}, size={size}");
    }

    /// <summary>
    /// 测试流式图片生成
    /// 验证点：
    /// - 返回 SSE 流
    /// - 包含 partial_image 事件（预览图）
    /// - 包含 completed 事件（最终图片）
    /// - completed 事件包含 usage 信息
    /// </summary>
    [Theory]
    [MemberData(nameof(GetImageGenerationModels))]
    public async Task ImageGeneration_Streaming_ShouldReturnImage(string model)
    {
        _output.WriteLine($"Testing: v1/images/generations (model: {model}, stream: true)");

        // Arrange
        var request = new
        {
            prompt = "Generate an image of a beautiful sunset",
            model,
            n = 1,
            quality = "low",  // 使用 low 来加快生成速度
            size = "1024x1024",
            stream = true,
            partial_images = 3
        };

        // Act
        HttpRequestMessage requestMessage = new(HttpMethod.Post, $"{_fixture.Config.OpenAICompatibleEndpoint}/images/generations")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };

        HttpResponseMessage response = await _fixture.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine("Streaming response:");

        Stream sseStream = await response.Content.ReadAsStreamAsync();
        int partialImageCount = 0;
        bool hasCompletedImage = false;
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
                        Assert.NotNull(chunk);

                        string? eventType = chunk["type"]?.GetValue<string>();
                        _output.WriteLine($"Event type: {eventType}");

                        if (eventType == "image_generation.partial_image")
                        {
                            // 验证 partial_image 事件
                            partialImageCount++;
                            int? partialImageIndex = chunk["partial_image_index"]?.GetValue<int>();
                            string? b64Json = chunk["b64_json"]?.GetValue<string>();
                            string? outputFormat = chunk["output_format"]?.GetValue<string>();

                            Assert.NotNull(partialImageIndex);
                            Assert.NotNull(b64Json);
                            Assert.NotEmpty(b64Json);

                            _output.WriteLine($"  Partial image #{partialImageCount}: index={partialImageIndex}, b64_json length={b64Json.Length}, format={outputFormat}");
                        }
                        else if (eventType == "image_generation.completed")
                        {
                            // 验证 completed 事件
                            hasCompletedImage = true;
                            string? b64Json = chunk["b64_json"]?.GetValue<string>();
                            string? outputFormat = chunk["output_format"]?.GetValue<string>();
                            JsonNode? usage = chunk["usage"];

                            Assert.NotNull(b64Json);
                            Assert.NotEmpty(b64Json);

                            _output.WriteLine($"  Completed image: b64_json length={b64Json.Length}, format={outputFormat}");

                            // 验证 usage
                            if (usage != null)
                            {
                                finalUsage = usage;
                                int? inputTokens = usage["input_tokens"]?.GetValue<int>();
                                int? outputTokens = usage["output_tokens"]?.GetValue<int>();
                                int? totalTokens = usage["total_tokens"]?.GetValue<int>();
                                _output.WriteLine($"  Usage: input_tokens={inputTokens}, output_tokens={outputTokens}, total_tokens={totalTokens}");
                            }

                            // 验证是否为有效的 base64
                            try
                            {
                                byte[] imageBytes = Convert.FromBase64String(b64Json);
                                Assert.True(imageBytes.Length > 0, "Decoded image should have content");
                                _output.WriteLine($"  Decoded image size: {imageBytes.Length} bytes");
                            }
                            catch (FormatException)
                            {
                                Assert.Fail("b64_json should be valid base64 encoded data");
                            }
                        }
                        else if (eventType == "error")
                        {
                            // 错误事件
                            JsonNode? error = chunk["error"];
                            string? errorMessage = error?["message"]?.GetValue<string>();
                            string? errorCode = error?["code"]?.GetValue<string>();
                            _output.WriteLine($"  Error: code={errorCode}, message={errorMessage}");
                            Assert.Fail($"Received error event: {errorMessage}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _output.WriteLine($"Failed to parse SSE data: {ex.Message}");
                        _output.WriteLine($"Data: {sse.Data[..Math.Min(200, sse.Data.Length)]}...");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Stream error: {ex.GetType().Name}: {ex.Message}");
            // 如果已经收到完成的图片内容，即使连接提前结束也认为测试通过
            if (!hasCompletedImage)
            {
                throw;
            }
            _output.WriteLine("Stream ended prematurely but we already received completed image, test passes");
        }

        Assert.True(hasCompletedImage, "Stream should contain a completed image event");
        _output.WriteLine($"Total partial images received: {partialImageCount}");

        // 验证 usage
        Assert.NotNull(finalUsage);
        int? finalInputTokens = finalUsage["input_tokens"]?.GetValue<int>();
        int? finalOutputTokens = finalUsage["output_tokens"]?.GetValue<int>();
        int? finalTotalTokens = finalUsage["total_tokens"]?.GetValue<int>();

        Assert.NotNull(finalInputTokens);
        Assert.NotNull(finalOutputTokens);
        Assert.NotNull(finalTotalTokens);
        Assert.True(finalInputTokens > 0, "input_tokens should be greater than 0");
        Assert.True(finalOutputTokens > 0, "output_tokens should be greater than 0");
        Assert.Equal(finalInputTokens + finalOutputTokens, finalTotalTokens);
    }

    /// <summary>
    /// 测试多张图片生成（n > 1）
    /// 验证点：
    /// - 当 n > 1 时，应返回多张图片
    /// - data 数组长度应等于 n
    /// </summary>
    [Theory]
    [MemberData(nameof(GetImageGenerationModels))]
    public async Task ImageGeneration_MultipleImages_ShouldReturnNImages(string model)
    {
        const int imageCount = 2;
        _output.WriteLine($"Testing: v1/images/generations with n={imageCount} (model: {model})");

        // Arrange
        var request = new
        {
            prompt = "Generate an image of a mountain landscape",
            model,
            n = imageCount,
            quality = "low",
            size = "1024x1024"
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/images/generations",
            request,
            _jsonOptions);

        // Assert
        await response.EnsureSuccessStatusCodeWithDetailsAsync();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(result);

        _output.WriteLine($"Status: {response.StatusCode}");

        // 验证 data 数组长度
        JsonNode? data = result["data"];
        Assert.NotNull(data);
        Assert.True(data is JsonArray, "data should be an array");

        JsonArray dataArray = (JsonArray)data;
        Assert.Equal(imageCount, dataArray.Count);
        _output.WriteLine($"Data array length: {dataArray.Count} (expected: {imageCount})");

        // 验证每张图片
        for (int i = 0; i < dataArray.Count; i++)
        {
            JsonNode? item = dataArray[i];
            Assert.NotNull(item);
            string? b64Json = item["b64_json"]?.GetValue<string>();
            Assert.NotNull(b64Json);
            Assert.NotEmpty(b64Json);
            _output.WriteLine($"Image {i + 1}: b64_json length={b64Json.Length} characters");
        }
    }

    /// <summary>
    /// 测试错误情况：缺少 prompt
    /// </summary>
    [Fact]
    public async Task ImageGeneration_MissingPrompt_ShouldReturnError()
    {
        string[] models = [.. _fixture.Config.Tests.ImageGenerationModels];
        if (models.Length == 0)
        {
            _output.WriteLine("No image generation models configured, skipping test");
            return;
        }

        string model = models[0];
        _output.WriteLine($"Testing: v1/images/generations without prompt (model: {model})");

        // Arrange - 故意不提供 prompt
        var request = new
        {
            model,
            n = 1
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/images/generations",
            request,
            _jsonOptions);

        // Assert - 应返回 400 Bad Request
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine($"Status: {response.StatusCode} (expected: BadRequest)");

        string content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Response: {content}");

        // 验证错误响应格式
        JsonObject? errorResponse = JsonSerializer.Deserialize<JsonObject>(content);
        Assert.NotNull(errorResponse);
        Assert.NotNull(errorResponse["error"]);
    }

    /// <summary>
    /// 测试错误情况：无效的模型
    /// </summary>
    [Fact]
    public async Task ImageGeneration_InvalidModel_ShouldReturnError()
    {
        _output.WriteLine("Testing: v1/images/generations with invalid model");

        // Arrange
        var request = new
        {
            prompt = "Generate an image",
            model = "non-existent-model-12345",
            n = 1
        };

        // Act
        HttpResponseMessage response = await _fixture.Client.PostAsJsonAsync(
            $"{_fixture.Config.OpenAICompatibleEndpoint}/images/generations",
            request,
            _jsonOptions);

        // Assert - 应返回 400 Bad Request
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine($"Status: {response.StatusCode} (expected: BadRequest)");

        string content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Response: {content}");
    }

    public static IEnumerable<object[]> GetImageGenerationModels()
    {
        ApiTestFixture fixture = new();
        return fixture.Config.Tests.ImageGenerationModels.Select(m => new object[] { m });
    }
}
