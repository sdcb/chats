using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChatTokenUsage = Chats.BE.Services.Models.Dtos.ChatTokenUsage;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ImageGenerationService(IHttpClientFactory httpClientFactory) : ChatService
{
    protected virtual string GetEndpoint(ModelKey modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
        }
        return host?.TrimEnd('/') ?? "";
    }

    protected virtual void AddAuthorizationHeader(HttpRequestMessage request, ModelKey modelKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", modelKey.Secret);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string prompt = GetPromptStatic(request.Messages);
        NeutralContent[] images = GetImagesStatic(request.Messages);

        // n>1 does not support streaming
        int n = request.ChatConfig.MaxOutputTokens ?? 1;
        if (n > 1)
        {
            Console.WriteLine("ImageGenerationService.ChatStreamed: n > 1, fallback to non-streaming Chat method.");
            (List<ChatSegment> imagesGenerated, ChatTokenUsage? usage) = await GenerateImagesAsync(request, cancellationToken);
            foreach (ChatSegment image in imagesGenerated)
            {
                yield return image;
            }
            if (usage != null)
            {
                yield return ChatSegment.FromUsage(usage);
            }
            yield return ChatSegment.FromFinishReason(DBFinishReason.Success);
            yield break;
        }

        string endpoint = GetEndpoint(request.ChatConfig.Model.ModelKey);

        if (images.Length == 0)
        {
            // Generate images API with streaming
            JsonObject requestBody = new()
            {
                ["prompt"] = prompt,
                ["model"] = request.ChatConfig.Model.DeploymentName,
                ["n"] = n,
                ["stream"] = true,
                ["partial_images"] = 3,
                ["moderation"] = "low"
            };

            if (request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
            {
                requestBody["quality"] = request.ChatConfig.ReasoningEffort.ToGeneratedImageQualityText();
            }

            if (!string.IsNullOrEmpty(request.ChatConfig.ImageSize))
            {
                requestBody["size"] = request.ChatConfig.ImageSize;
            }

            if (request.EndUserId != null)
            {
                requestBody["user"] = request.EndUserId;
            }

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/images/generations");
            AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
            httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using HttpClient httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = NetworkTimeout;

            Stopwatch sw = Stopwatch.StartNew();
            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new RawChatServiceException((int)response.StatusCode, errorBody);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (ChatSegment segment in ProcessImageStreamResponseAsync(stream, sw, cancellationToken))
            {
                yield return segment;
            }
        }
        else
        {
            // Image edits API with streaming
            using MultipartFormDataContent form = await BuildImageEditFormAsync(images, prompt, request, cancellationToken);
            form.Add(new StringContent("true"), "stream");
            form.Add(new StringContent("3"), "partial_images");

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/images/edits");
            AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
            httpRequest.Content = form;
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using HttpClient httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = NetworkTimeout;

            Stopwatch sw = Stopwatch.StartNew();
            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new RawChatServiceException((int)response.StatusCode, errorBody);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (ChatSegment segment in ProcessImageStreamResponseAsync(stream, sw, cancellationToken))
            {
                yield return segment;
            }
        }
    }

    private async Task<(List<ChatSegment> Segments, ChatTokenUsage? Usage)> GenerateImagesAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        string prompt = GetPromptStatic(request.Messages);
        NeutralContent[] images = GetImagesStatic(request.Messages);
        string endpoint = GetEndpoint(request.ChatConfig.Model.ModelKey);

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = NetworkTimeout;

        JsonObject rawJson;
        if (images.Length == 0)
        {
            JsonObject requestBody = new()
            {
                ["prompt"] = prompt,
                ["model"] = request.ChatConfig.Model.DeploymentName,
                ["n"] = request.ChatConfig.MaxOutputTokens ?? 1,
                ["moderation"] = "low"
            };

            if (request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
            {
                requestBody["quality"] = request.ChatConfig.ReasoningEffort.ToGeneratedImageQualityText();
            }

            if (!string.IsNullOrEmpty(request.ChatConfig.ImageSize))
            {
                requestBody["size"] = request.ChatConfig.ImageSize;
            }

            if (request.EndUserId != null)
            {
                requestBody["user"] = request.EndUserId;
            }

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/images/generations");
            AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
            httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new RawChatServiceException((int)response.StatusCode, errorBody);
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            rawJson = JsonSerializer.Deserialize<JsonObject>(responseJson) ?? throw new Exception("Unable to parse raw JSON from the response.");
        }
        else
        {
            using MultipartFormDataContent form = await BuildImageEditFormAsync(images, prompt, request, cancellationToken);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/images/edits");
            AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
            httpRequest.Content = form;

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new RawChatServiceException((int)response.StatusCode, errorBody);
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            rawJson = JsonSerializer.Deserialize<JsonObject>(responseJson) ?? throw new Exception("Unable to parse raw JSON from the response.");
        }

        JsonNode? usage = rawJson["usage"];
        JsonArray? data = rawJson["data"]?.AsArray();

        List<ChatSegment> items = [];
        if (data != null)
        {
            foreach (JsonNode? item in data)
            {
                string? b64Json = item?["b64_json"]?.GetValue<string>();
                if (b64Json != null)
                {
                    items.Add(ChatSegment.FromBase64Image(b64Json, "image/png"));
                }
            }
        }

        ChatTokenUsage? usageInfo = usage != null ? new ChatTokenUsage()
        {
            InputTokens = usage["input_tokens"]?.GetValue<int>() ?? 0,
            OutputTokens = usage["output_tokens"]?.GetValue<int>() ?? 0,
            ReasoningTokens = 0,
            CacheTokens = 0,
        } : null;

        return (items, usageInfo);
    }

    private static string GetPromptStatic(IList<NeutralMessage> messages)
    {
        NeutralMessage? userMessage = messages.LastUserMessage();

        if (userMessage != null)
        {
            string textPart = userMessage.Contents
                .OfType<NeutralTextContent>()
                .Select(x => x.Content)
                .LastOrDefault() ?? throw new CustomChatServiceException(DBFinishReason.BadParameter, $"Unable to find a text part in the user message.");
            return textPart;
        }
        else
        {
            throw new CustomChatServiceException(DBFinishReason.BadParameter, "Unable to find the user message in the messages.");
        }
    }

    private static NeutralContent[] GetImagesStatic(IList<NeutralMessage> messages)
    {
        // latest message is always the user message
        // if user message contains image, we need to use all images in the message as input
        NeutralMessage? userMessage = messages.LastUserMessage();
        if (userMessage != null)
        {
            NeutralContent[] userMessageImages = [.. userMessage.Contents.Where(x => x.IsFile)];
            if (userMessageImages.Length > 0)
            {
                return userMessageImages;
            }
        }

        // otherwise, we need to use the last image in the message as input
        return [.. messages.SelectMany(x => x.Contents)
            .Where(x => x.IsFile)
            .Reverse()
            .Take(1)];
    }

    private static async Task<MultipartFormDataContent> BuildImageEditFormAsync(
        NeutralContent[] images,
        string prompt,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        using HttpClient http = new();
        MultipartFormDataContent form = new();

        Dictionary<string, HttpResponseMessage> downloadedFiles = (await Task.WhenAll(images
            .OfType<NeutralFileUrlContent>()
            .Select(async imageUrl =>
            {
                HttpResponseMessage resp = await http.GetAsync(imageUrl.Url, cancellationToken);
                return (url: imageUrl.Url, resp);
            })))
        .ToDictionary(k => k.url, v => v.resp);

        foreach (NeutralContent image in images)
        {
            if (image is NeutralFileUrlContent fileUrl)
            {
                HttpResponseMessage file = downloadedFiles[fileUrl.Url];
                string fileName = Path.GetFileName(new Uri(fileUrl.Url).LocalPath);
                Stream fileStream = await file.Content.ReadAsStreamAsync(cancellationToken);
                StreamContent streamContent = new(fileStream);
                streamContent.Headers.ContentType = file.Content.Headers.ContentType;

                if (fileName.Contains("mask.png"))
                {
                    form.Add(streamContent, "mask", fileName);
                }
                else
                {
                    form.Add(streamContent, "image", fileName);
                }
            }
            else if (image is NeutralFileBlobContent blob)
            {
                ByteArrayContent content = new(blob.Data);
                content.Headers.ContentType = new MediaTypeHeaderValue(blob.MediaType);
                form.Add(content, "image", DBFileDef.MakeFileNameByContentType(blob.MediaType));
            }
        }

        form.Add(new StringContent(prompt), "prompt");
        form.Add(new StringContent((request.ChatConfig.MaxOutputTokens ?? 1).ToString()), "n");
        form.Add(new StringContent(request.ChatConfig.Model.DeploymentName), "model");

        if (!string.IsNullOrEmpty(request.ChatConfig.ImageSize))
        {
            form.Add(new StringContent(request.ChatConfig.ImageSize), "size");
        }

        if (request.EndUserId != null)
        {
            form.Add(new StringContent(request.EndUserId), "user");
        }

        form.Add(new StringContent("low"), "moderation");

        if (request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
        {
            form.Add(new StringContent(request.ChatConfig.ReasoningEffort.ToGeneratedImageQualityText()!), "quality");
        }

        return form;
    }

    private static string GetContentTypeFromOutputFormat(string outputFormat) => outputFormat.ToLowerInvariant() switch
    {
        "png" => "image/png",
        "jpeg" => "image/jpeg",
        "jpg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/png", // default to png
    };

    private static async IAsyncEnumerable<ChatSegment> ProcessImageStreamResponseAsync(
        Stream stream,
        Stopwatch sw,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int eventIndex = 0;

        await foreach (SseItem<string> item in SseParser.Create(stream, (_, bytes) => Encoding.UTF8.GetString(bytes)).EnumerateAsync(cancellationToken))
        {
            if (string.IsNullOrEmpty(item.Data) || item.Data == "[DONE]")
            {
                continue;
            }

            JsonDocument doc = JsonDocument.Parse(item.Data);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("type", out JsonElement typeElement))
            {
                continue;
            }

            string eventType = typeElement.GetString()!;

            if (eventType == "image_generation.partial_image" || eventType == "image_edit.partial_image")
            {
                string b64Json = root.GetProperty("b64_json").GetString()!;
                string outputFormat = root.GetProperty("output_format").GetString()!;
                string contentType = GetContentTypeFromOutputFormat(outputFormat);

                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}");

                yield return ChatSegment.FromBase64PreviewImage(b64Json, contentType);
            }
            else if (eventType == "image_generation.completed" || eventType == "image_edit.completed")
            {
                string b64Json = root.GetProperty("b64_json").GetString()!;
                string outputFormat = root.GetProperty("output_format").GetString()!;
                string contentType = GetContentTypeFromOutputFormat(outputFormat);

                JsonElement usageElement = root.GetProperty("usage");
                ChatTokenUsage usage = new()
                {
                    InputTokens = usageElement.GetProperty("input_tokens").GetInt32(),
                    OutputTokens = usageElement.GetProperty("output_tokens").GetInt32(),
                    ReasoningTokens = 0,
                    CacheTokens = 0,
                };

                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}, input={usage.InputTokens}, output={usage.OutputTokens}");

                yield return ChatSegment.FromBase64Image(b64Json, contentType);
                if (usage != null)
                {
                    yield return ChatSegment.FromUsage(usage);
                }
                yield return ChatSegment.FromFinishReason(DBFinishReason.Success);
            }
            else if (eventType == "error")
            {
                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}");
                throw new RawChatServiceException(200, root.ToString());
            }
            else
            {
                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] Unknown event type: {eventType} #{eventIndex}");
            }

            eventIndex++;
        }
    }
}
