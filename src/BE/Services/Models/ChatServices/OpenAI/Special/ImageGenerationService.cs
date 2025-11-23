using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
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
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(model.ModelKey, perCallPolicies);
        ImageClient cc = api.GetImageClient(model.DeploymentName);
        return cc;
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ImageClient imageClient = CreateImageGenerationAPI(request.ChatConfig.Model, []);

        string prompt = GetPromptStatic(request.Steps);
        StepContent[] images = GetImagesStatic(request.Steps);

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

            if ((DBReasoningEffort)request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
            {
                requestBody["quality"] = ((DBReasoningEffort)request.ChatConfig.ReasoningEffort).ToGeneratedImageQualityText();
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
        string prompt = GetPromptStatic(request.Steps);
        StepContent[] images = GetImagesStatic(request.Steps);
        ClientResult<GeneratedImageCollection> cr = null!;
        if (images.Length == 0)
        {
            cr = await imageClient.GenerateImagesAsync(
                prompt,
                request.ChatConfig.MaxOutputTokens ?? 1,
                new ImageGenerationOptions()
                {
                    EndUserId = request.EndUserId,
                    Quality = ((DBReasoningEffort)request.ChatConfig.ReasoningEffort).ToGeneratedImageQuality(),
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

    private static string GetPromptStatic(IEnumerable<Step> steps)
    {
        Step? userMessage = steps.LastUserMessage;

        if (userMessage != null)
        {
            string textPart = userMessage.StepContents
                .Where(x => (DBStepContentType)x.ContentTypeId == DBStepContentType.Text && x.StepContentText != null)
                .Select(x => x.StepContentText!.Content)
                .LastOrDefault() ?? throw new InvalidOperationException($"Unable to find a text part in the user message.");
            return textPart;
        }
        else
        {
            throw new InvalidOperationException("Unable to find the user message in the steps.");
        }
    }

    private static StepContent[] GetImagesStatic(ICollection<Step> steps)
    {
        // latest message is always the user message
        // if user message contains image, we need to use all images in the message as input
        Step? userMessage = steps.LastUserMessage;
        if (userMessage != null)
        {
            StepContent[] userMessageImages = [.. userMessage.StepContents.Where(x => x.IsFile())];
            if (userMessageImages.Length > 0)
            {
                return userMessageImages;
            }
        }

        // otherwise, we need to use the last image in the message as input
        return [.. steps.SelectMany(x => x.StepContents)
            .Where(x => x.IsFile())
            .Reverse()
            .Take(1)];
    }

    private async Task<MultiPartFormDataBinaryContent> BuildImageEditFormAsync(
        StepContent[] images,
        string prompt,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        using HttpClient http = new();
        MultiPartFormDataBinaryContent form = new();

        Dictionary<string, HttpResponseMessage> downloadedFiles = (await Task.WhenAll(images
            .Select(x => x.TryGetFileUrl(out string? url) ? url : null)
            .Where(x => x != null)
            .Select(async imageUrl =>
            {
                HttpResponseMessage resp = await http.GetAsync(imageUrl, cancellationToken);
                return (url: imageUrl!, resp);
            })))
        .ToDictionary(k => k.url, v => v.resp);

        foreach (StepContent image in images)
        {
            if (image.TryGetFileUrl(out string? url))
            {
                HttpResponseMessage file = downloadedFiles[url];
                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                if (fileName.Contains("mask.png"))
                {
                    form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "mask", fileName, file.Content.Headers.ContentType?.ToString());
                }
                else
                {
                    form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "image", fileName, file.Content.Headers.ContentType?.ToString());
                }
            }
            else if (image.TryGetFileBlob(out StepContentBlob? blob))
            {
                form.Add(blob.Content, "image", DBFileDef.MakeFileNameByContentType(blob.MediaType), blob.MediaType);
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

        if ((DBReasoningEffort)request.ChatConfig.ReasoningEffort != DBReasoningEffort.Default)
        {
            form.Add(((DBReasoningEffort)request.ChatConfig.ReasoningEffort).ToGeneratedImageQualityText()!, "quality");
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

            // Partial image event
            // Format: 
            // {"type":"image_generation.partial_image","partial_image_index":0,"b64_json":"...","created_at":1761112880,"size":"1024x1024","quality":"high","background":"opaque","output_format":"png"}
            // Format: 
            // {
            //   "type": "image_edit.partial_image",
            //   "partial_image_index": 0,
            //   "b64_json": "...",
            //   "created_at": 1761116613,
            //   "size": "1024x1024",
            //   "quality": "high",
            //   "background": "opaque",
            //   "output_format": "png"
            // }
            if (eventType == "image_generation.partial_image" || eventType == "image_edit.partial_image")
            {
                string b64Json = root.GetProperty("b64_json").GetString()!;
                string outputFormat = root.GetProperty("output_format").GetString()!;
                string contentType = GetContentTypeFromOutputFormat(outputFormat);

                Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F3}s] {eventType} #{eventIndex}");

                // Yield partial image
                yield return new ChatSegment()
                {
                    FinishReason = null,
                    Items = [ChatSegmentItem.FromBase64PreviewImage(b64Json, contentType)],
                    Usage = null,
                };
            }
            // Completed image event
            // image_generation.completed:
            // {
            //   "type":"image_generation.completed",
            //   "b64_json":"...",
            //   "created_at":1761105722,
            //   "usage": {
            //     "input_tokens": 7,
            //     "output_tokens": 4460,
            //     "total_tokens": 4467,
            //     "input_tokens_details":{
            //       "text_tokens": 7,
            //       "image_tokens": 0
            //     }
            //   },
            //   "size":"1024x1024",
            //   "quality":"high",
            //   "background":"opaque",
            //   "output_format":"png"
            // }
            // image_edit.completed:
            // {
            //     "type": "image_edit.completed",
            //     "b64_json": "...",
            //     "created_at": 1761116646,
            //     "usage": {
            //         "input_tokens": 227,
            //         "output_tokens": 4460,
            //         "total_tokens": 4687,
            //         "input_tokens_details": {
            //             "text_tokens": 33,
            //             "image_tokens": 194
            //         }
            //     },
            //     "size": "1024x1024",
            //     "quality": "high",
            //     "background": "opaque",
            //     "output_format": "png"
            // }
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

                // Yield final complete image with usage
                yield return new ChatSegment()
                {
                    FinishReason = ChatFinishReason.Stop,
                    Items = [ChatSegmentItem.FromBase64Image(b64Json, contentType)],
                    Usage = usage,
                };
            }
            //event: error
            //data: {"type":"error","error":{"type":"image_generation_server_error","code":"image_generation_failed","message":"Image generation failed","param":null}}
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
