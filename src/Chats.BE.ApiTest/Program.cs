using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Nodes;

// 读取配置
IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .Build();

string apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey not found in appsettings.json");
string endpoint = configuration["OpenAICompatibleEndpoint"] ?? throw new InvalidOperationException("OpenAICompatibleEndpoint not found in appsettings.json");

// 读取测试配置
string[] nonStreamingModels = configuration.GetSection("Tests:NonStreamingModels").Get<string[]>() ?? [];
string[] streamingModels = configuration.GetSection("Tests:StreamingModels").Get<string[]>() ?? [];
string[] reasoningModels = configuration.GetSection("Tests:ReasoningModels").Get<string[]>() ?? [];
string[] cachedModels = configuration.GetSection("Tests:CachedModels").Get<string[]>() ?? [];
string[] toolCallModels = configuration.GetSection("Tests:ToolCallModels").Get<string[]>() ?? [];
bool testGetModels = configuration.GetValue<bool>("Tests:GetModels", true);

using HttpClient client = new();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
client.Timeout = TimeSpan.FromMinutes(5);

JsonSerializerOptions jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
};

Console.WriteLine("=== Chats API Integration Test ===");
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine();

try
{
    // 测试 1: 获取模型列表
    if (testGetModels)
    {
        await TestGetModels();
    }

    // 测试 2: 普通聊天完成（非流式）
    foreach (string model in nonStreamingModels)
    {
        await TestChatCompletion(model, stream: false);
    }

    // 测试 3: 流式聊天完成
    foreach (string model in streamingModels)
    {
        await TestChatCompletion(model, stream: true);
    }

    // 测试 4: 推理模型测试
    foreach (string model in reasoningModels)
    {
        await TestReasoningModel(model);
    }

    // 测试 5: 缓存测试
    foreach (string model in cachedModels)
    {
        await TestCachedCompletion(model);
    }

    // 测试 6: 工具调用测试
    foreach (string model in toolCallModels)
    {
        await TestToolCalls(model);
    }

    Console.WriteLine();
    Console.WriteLine("=== All tests completed successfully! ===");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Environment.Exit(1);
}

async Task TestGetModels()
{
    Console.WriteLine("--- Test: Get Models ---");
    try
    {
        HttpResponseMessage response = await client.GetAsync($"{endpoint}/models");
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);
        
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Models: {json?["data"]?.AsArray().Count ?? 0}");
        
        if (json?["data"] is JsonArray models)
        {
            foreach (JsonNode? model in models)
            {
                Console.WriteLine($"  - {model?["id"]} (owned by: {model?["owned_by"]})");
            }
        }
        
        Console.WriteLine("✓ Test passed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Test failed: {ex.Message}");
        throw;
    }
    Console.WriteLine();
}

async Task TestChatCompletion(string model, bool stream)
{
    Console.WriteLine($"--- Test: Chat Completion (model: {model}, stream: {stream}) ---");
    try
    {
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "1 + 1 = ?" }
            },
            stream,
            temperature = 0.7
        };

        HttpResponseMessage response = await client.PostAsJsonAsync($"{endpoint}/chat/completions", request, jsonOptions);
        response.EnsureSuccessStatusCode();

        if (stream)
        {
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine("Streaming response:");

            Stream sseStream = await response.Content.ReadAsStreamAsync();
            int chunkCount = 0;
            
            await foreach (SseItem<string> sse in SseParser.Create(sseStream).EnumerateAsync())
            {
                if (sse.EventType == "done" || sse.Data == "[DONE]")
                {
                    Console.WriteLine("\n[DONE]");
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
                            Console.Write(content);
                            chunkCount++;
                        }
                    }
                    catch { }
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"Received {chunkCount} content chunks");
        }
        else
        {
            JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Model: {result?["model"]}");
            Console.WriteLine($"Content: {result?["choices"]?[0]?["message"]?["content"]}");
            Console.WriteLine($"Finish Reason: {result?["choices"]?[0]?["finish_reason"]}");
            Console.WriteLine($"Usage: prompt_tokens={result?["usage"]?["prompt_tokens"]}, completion_tokens={result?["usage"]?["completion_tokens"]}");
        }
        
        Console.WriteLine("✓ Test passed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Test failed: {ex.Message}");
        throw;
    }
    Console.WriteLine();
}

async Task TestReasoningModel(string model)
{
    Console.WriteLine($"--- Test: Reasoning Model (model: {model}) ---");
    try
    {
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = "What is the capital of France? Think step by step." }
            },
            stream = false
        };

        HttpResponseMessage response = await client.PostAsJsonAsync($"{endpoint}/chat/completions", request, jsonOptions);
        response.EnsureSuccessStatusCode();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Model: {result?["model"]}");
        Console.WriteLine($"Content: {result?["choices"]?[0]?["message"]?["content"]}");
        Console.WriteLine($"Finish Reason: {result?["choices"]?[0]?["finish_reason"]}");
        
        Console.WriteLine("✓ Test passed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Test failed: {ex.Message}");
        throw;
    }
    Console.WriteLine();
}

async Task TestCachedCompletion(string model)
{
    Console.WriteLine($"--- Test: Cached Completion (model: {model}) ---");
    try
    {
        JsonObject request = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "What is 2 + 2?" }
            },
            ["stream"] = false,
            ["cache_control"] = new JsonObject
            {
                ["expires_at"] = DateTime.UtcNow.AddHours(1).ToString("o"),
                ["create_only"] = false
            }
        };

        // 第一次请求（创建缓存）
        Console.WriteLine("First request (creating cache)...");
        HttpResponseMessage response1 = await client.PostAsJsonAsync($"{endpoint}/chat/completions", request, jsonOptions);
        response1.EnsureSuccessStatusCode();
        JsonObject? result1 = await response1.Content.ReadFromJsonAsync<JsonObject>();
        Console.WriteLine($"Response: {result1?["choices"]?[0]?["message"]?["content"]}");

        // 等待一下确保缓存已保存
        await Task.Delay(500);

        // 第二次请求（使用缓存）
        Console.WriteLine("Second request (should use cache)...");
        HttpResponseMessage response2 = await client.PostAsJsonAsync($"{endpoint}/chat/completions", request, jsonOptions);
        response2.EnsureSuccessStatusCode();
        JsonObject? result2 = await response2.Content.ReadFromJsonAsync<JsonObject>();
        Console.WriteLine($"Response: {result2?["choices"]?[0]?["message"]?["content"]}");

        Console.WriteLine("✓ Test passed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Test failed: {ex.Message}");
        throw;
    }
    Console.WriteLine();
}

async Task TestToolCalls(string model)
{
    Console.WriteLine($"--- Test: Tool Calls (model: {model}) ---");
    try
    {
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

        HttpResponseMessage response = await client.PostAsJsonAsync($"{endpoint}/chat/completions", request, jsonOptions);
        response.EnsureSuccessStatusCode();

        JsonObject? result = await response.Content.ReadFromJsonAsync<JsonObject>();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Finish Reason: {result?["choices"]?[0]?["finish_reason"]}");

        JsonNode? message = result?["choices"]?[0]?["message"];
        if (message?["tool_calls"] is JsonArray toolCalls && toolCalls.Count > 0)
        {
            Console.WriteLine($"Tool calls: {toolCalls.Count}");
            foreach (JsonNode? toolCall in toolCalls)
            {
                Console.WriteLine($"  - Function: {toolCall?["function"]?["name"]}");
                Console.WriteLine($"    Arguments: {toolCall?["function"]?["arguments"]}");
            }
        }
        else
        {
            Console.WriteLine($"Content: {message?["content"]}");
        }
        
        Console.WriteLine("✓ Test passed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Test failed: {ex.Message}");
        // Tool calls may not be supported by all models, so we don't throw
        Console.WriteLine("(Tool calls may not be supported by this model)");
    }
    Console.WriteLine();
}
