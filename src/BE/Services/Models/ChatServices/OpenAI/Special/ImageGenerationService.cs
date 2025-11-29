using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeneratedImageSize = OpenAI.Images.GeneratedImageSize;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ImageGenerationService : ChatService
{
    protected virtual ImageClient CreateImageGenerationAPI(Model model, PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = CreateOpenAIClient(model.ModelKey, perCallPolicies);
        ImageClient cc = api.GetImageClient(model.DeploymentName);
        return cc;
    }

    protected virtual OpenAIClient CreateOpenAIClient(ModelKey modelKey, params PipelinePolicy[] perCallPolicies)
    {
        return OpenAIHelper.BuildOpenAIClient(modelKey, perCallPolicies);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ImageClient imageClient = CreateImageGenerationAPI(request.ChatConfig.Model, []);

        string prompt = GetPromptStatic(request.Messages);
        NeutralContent[] images = GetImagesStatic(request.Messages);

        // 兼容 n>1 时不走 stream
        int n = request.ChatConfig.MaxOutputTokens ?? 1;
        if (n > 1)
        {
            // fallback 到 Chat 方法
            Console.WriteLine("ImageGenerationService.ChatStreamed: n > 1, fallback to non-streaming Chat method.");
            ChatSegment result = await Chat(request, cancellationToken);
            yield return result;
            yield break;
        }

        ClientPipeline pipeline = imageClient.Pipeline;

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

            BinaryContent content = BinaryContent.Create(BinaryData.FromString(requestBody.ToJsonString()));
            PipelineMessage message = pipeline.CreateMessage();
            message.Request.Method = "POST";
            message.Request.Uri = new Uri(imageClient.Endpoint, "v1/images/generations");
            message.Request.Headers.Set("Content-Type", "application/json");
            message.Request.Headers.Set("Accept", "text/event-stream");
            message.Request.Content = content;
            message.BufferResponse = false;

            Stopwatch sw = Stopwatch.StartNew();
            await pipeline.SendAsync(message).ConfigureAwait(false);

            // Check response status
            if (message.Response == null)
            {
                throw new Exception("No response received from the server.");
            }

            if (!message.Response.IsError)
            {
                await foreach (ChatSegment segment in ProcessImageStreamResponseAsync(message.Response, sw, cancellationToken))
                {
                    yield return segment;
                }
            }
            else
            {
                await ThrowErrorResponse(message.Response, cancellationToken);
            }
        }
        else
        {
            // Image edits API with streaming
            MultiPartFormDataBinaryContent form = await BuildImageEditFormAsync(images, prompt, request, cancellationToken);
            form.Add("true", "stream");
            form.Add("3", "partial_images");

            PipelineMessage message = pipeline.CreateMessage();
            message.Request.Method = "POST";
            message.Request.Uri = new Uri(imageClient.Endpoint, "v1/images/edits");
            message.Request.Headers.Set("Content-Type", form.ContentType);
            message.Request.Headers.Set("Accept", "text/event-stream");
            message.Request.Content = form;
            message.BufferResponse = false;

            Stopwatch sw = Stopwatch.StartNew();
            await pipeline.SendAsync(message).ConfigureAwait(false);

            // Check response status
            if (message.Response == null)
            {
                throw new Exception("No response received from the server.");
            }

            if (!message.Response.IsError)
            {
                await foreach (ChatSegment segment in ProcessImageStreamResponseAsync(message.Response, sw, cancellationToken))
                {
                    yield return segment;
                }
            }
            else
            {
                await ThrowErrorResponse(message.Response, cancellationToken);
            }
        }

        yield break;
    }

    public override async Task<ChatSegment> Chat(ChatRequest request, CancellationToken cancellationToken)
    {
        ImageClient imageClient = CreateImageGenerationAPI(request.ChatConfig.Model, []);
        string prompt = GetPromptStatic(request.Messages);
        NeutralContent[] images = GetImagesStatic(request.Messages);
        ClientResult<GeneratedImageCollection> cr = null!;
        if (images.Length == 0)
        {
            cr = await imageClient.GenerateImagesAsync(
                prompt,
                request.ChatConfig.MaxOutputTokens ?? 1,
                new ImageGenerationOptions()
                {
                    EndUserId = request.EndUserId,
                    Quality = request.ChatConfig.ReasoningEffort.ToGeneratedImageQuality(),
                    Size = string.IsNullOrEmpty(request.ChatConfig.ImageSize) ? null : new GeneratedImageSize(request.ChatConfig.ImageSize),
                    ModerationLevel = GeneratedImageModerationLevel.Low,
                }, cancellationToken);
        }
        else
        {
            MultiPartFormDataBinaryContent form = await BuildImageEditFormAsync(images, prompt, request, cancellationToken);

            ClientResult clientResult = await imageClient.GenerateImageEditsAsync(form, form.ContentType, new RequestOptions()
            {
                CancellationToken = cancellationToken
            });
            cr = ClientResult.FromValue((GeneratedImageCollection)clientResult, clientResult.GetRawResponse());
        }

        JsonObject rawJson = cr.GetRawResponse().Content
            .ToObjectFromJson<JsonObject>() ?? throw new Exception("Unable to parse raw JSON from the response.");
        GeneratedImageCollection gic = cr.Value ?? throw new Exception("Unable to parse generated image collection from the response.");
        JsonNode usage = rawJson["usage"] ?? throw new Exception("Unable to parse usage from the response.");

        return new ChatSegment()
        {
            FinishReason = null,
            Items = [.. cr.Value.Select(x => ChatSegmentItem.FromBinaryData(x.ImageBytes, "image/png"))],
            Usage = new Dtos.ChatTokenUsage()
            {
                InputTokens = usage["input_tokens"]!.GetValue<int>(),
                OutputTokens = usage["output_tokens"]!.GetValue<int>(),
                ReasoningTokens = 0,
            },
        };
    }

    private static string GetPromptStatic(IList<NeutralMessage> messages)
    {
        NeutralMessage? userMessage = messages.LastUserMessage();

        if (userMessage != null)
        {
            string textPart = userMessage.Contents
                .OfType<NeutralTextContent>()
                .Select(x => x.Content)
                .LastOrDefault() ?? throw new InvalidOperationException($"Unable to find a text part in the user message.");
            return textPart;
        }
        else
        {
            throw new InvalidOperationException("Unable to find the user message in the messages.");
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

    private async Task<MultiPartFormDataBinaryContent> BuildImageEditFormAsync(
        NeutralContent[] images,
        string prompt,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        using HttpClient http = new();
        MultiPartFormDataBinaryContent form = new();

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
                if (fileName.Contains("mask.png"))
                {
                    form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "mask", fileName, file.Content.Headers.ContentType?.ToString());
                }
                else
                {
                    form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "image", fileName, file.Content.Headers.ContentType?.ToString());
                }
            }
            else if (image is NeutralFileBlobContent blob)
            {
                form.Add(blob.Data, "image", DBFileDef.MakeFileNameByContentType(blob.MediaType), blob.MediaType);
            }
        }

        form.Add(prompt, "prompt");
        form.Add(request.ChatConfig.MaxOutputTokens ?? 1, "n");
        form.Add(request.ChatConfig.Model.DeploymentName, "model");

        if (!string.IsNullOrEmpty(request.ChatConfig.ImageSize))
        {
            form.Add(request.ChatConfig.ImageSize, "size");
        }

        if (request.EndUserId != null)
        {
            form.Add(request.EndUserId, "user");
        }

        form.Add("low", "moderation");

        if (request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
        {
            form.Add(request.ChatConfig.ReasoningEffort.ToGeneratedImageQualityText()!, "quality");
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
        PipelineResponse response,
        Stopwatch sw,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (response.ContentStream == null)
        {
            yield break;
        }

        int eventIndex = 0;

        await foreach (SseItem<string> item in SseParser.Create(response.ContentStream).EnumerateAsync(cancellationToken))
        {
            JsonDocument doc = JsonDocument.Parse(item.Data);
            JsonElement root = doc.RootElement;
            string eventType = root.GetProperty("type").GetString()!;

            if (eventType == "image_generation.partial_image" || eventType == "image_edit.partial_image")
            {
                string b64Json = root.GetProperty("b64_json").GetString()!;
                string outputFormat = root.GetProperty("output_format").GetString()!;
                string contentType = GetContentTypeFromOutputFormat(outputFormat);

                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}");

                yield return new ChatSegment()
                {
                    FinishReason = null,
                    Items = [ChatSegmentItem.FromBase64PreviewImage(b64Json, contentType)],
                    Usage = null,
                };
            }
            else if (eventType == "image_generation.completed" || eventType == "image_edit.completed")
            {
                string b64Json = root.GetProperty("b64_json").GetString()!;
                string outputFormat = root.GetProperty("output_format").GetString()!;
                string contentType = GetContentTypeFromOutputFormat(outputFormat);

                JsonElement usageElement = root.GetProperty("usage");
                Dtos.ChatTokenUsage usage = new()
                {
                    InputTokens = usageElement.GetProperty("input_tokens").GetInt32(),
                    OutputTokens = usageElement.GetProperty("output_tokens").GetInt32(),
                    ReasoningTokens = 0,
                };

                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}, input={usage.InputTokens}, output={usage.OutputTokens}");

                yield return new ChatSegment()
                {
                    FinishReason = ChatFinishReason.Stop,
                    Items = [ChatSegmentItem.FromBase64Image(b64Json, contentType)],
                    Usage = usage,
                };
            }
            else if (eventType == "error")
            {
                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}");
                throw new Exception(root.ToString());
            }
            else
            {
                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] Unknown event type: {eventType} #{eventIndex}");
            }

            eventIndex++;
        }
    }

    private static async Task ThrowErrorResponse(PipelineResponse response, CancellationToken cancellationToken)
    {
        if (response.ContentStream != null)
        {
            using StreamReader reader = new(response.ContentStream);
            string errorContent = await reader.ReadToEndAsync(cancellationToken);
            throw new Exception($"Request failed with status {response.Status}: {errorContent}");
        }
        else
        {
            throw new Exception($"Request failed with status {response.Status}: {response.ReasonPhrase}");
        }
    }
}
