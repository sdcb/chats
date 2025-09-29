using OpenAI;
using System.ClientModel;
using Chats.BE.DB;
using OpenAI.Images;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using Chats.BE.DB.Enums;
using System.Text.Json.Nodes;
using System.ClientModel.Primitives;
using Chats.BE.Services.FileServices;
using System.Runtime.CompilerServices;
using GeneratedImageSize = OpenAI.Images.GeneratedImageSize;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ImageGenerationService(Model model, ImageClient imageClient) : ChatService(model)
{
    private DBReasoningEffort _reasoningEffort;
    private DBKnownImageSize _imageSize;

    public ImageGenerationService(Model model, Uri? suggestedUri = null, params PipelinePolicy[] perCallPolicies) : this(model, CreateImageGenerationAPI(model, suggestedUri, perCallPolicies))
    {
    }

    private static ImageClient CreateImageGenerationAPI(Model model, Uri? suggestedUrl, PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(model, suggestedUrl, perCallPolicies);
        ImageClient cc = api.GetImageClient(model.ApiModelId);
        return cc;
    }

    public override IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override async Task<ChatSegment> Chat(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
    {
        string prompt = GetPrompt(messages);
        ChatMessageContentPart[] images = GetImages(messages);
        ClientResult<GeneratedImageCollection> cr = null!;
        if (images.Length == 0)
        {
            cr = await imageClient.GenerateImagesAsync(
                prompt,
                options.MaxOutputTokenCount ?? 1,
                new ImageGenerationOptions()
                {
                    EndUserId = options.EndUserId,
                    Quality = _reasoningEffort switch
                    {
                        DBReasoningEffort.Default => (GeneratedImageQuality?)null,
                        DBReasoningEffort.Low => "low",
                        DBReasoningEffort.Medium => "medium",
                        DBReasoningEffort.High => "high",
                        _ => throw new ArgumentOutOfRangeException(nameof(_reasoningEffort), _reasoningEffort, null)
                    },
                    Size = _imageSize switch
                    {
                        DBKnownImageSize.Default => prompt.Contains("3:2") ? GeneratedImageSize.W1536xH1024 : prompt.Contains("2:3") ? GeneratedImageSize.W1024xH1536 : null,
                        DBKnownImageSize.W1024xH1024 => GeneratedImageSize.W1024xH1024,
                        DBKnownImageSize.W1536xH1024 => GeneratedImageSize.W1536xH1024,
                        DBKnownImageSize.W1024xH1536 => GeneratedImageSize.W1024xH1536,
                        _ => throw new ArgumentOutOfRangeException(nameof(_imageSize), _imageSize, null)
                    },
                    ModerationLevel = GeneratedImageModerationLevel.Low,
                }, cancellationToken);
        }
        else
        {
            using HttpClient http = new();
            //Dictionary<Uri, HttpResponseMessage> downloadedFiles = (await Task.WhenAll(images
            //    .Where(x => x.ImageUri != null)
            //    .Take(2)
            //    .GroupBy(x => x.ImageUri)
            //    .Select(async image =>
            //    {
            //        HttpResponseMessage resp = await http.GetAsync(image.Key, cancellationToken);
            //        return (url: image.Key, resp);
            //    })))
            //.ToDictionary(k => k.url, v => v.resp);

            //// mask edit
            //Stream? imageStream = null, maskStream = null;
            //string? imageFile = null, maskFile = null;
            //foreach (KeyValuePair<Uri, HttpResponseMessage> kv in downloadedFiles)
            //{
            //    if (kv.Key.ToString().Contains("mask.png") && maskFile == null)
            //    {
            //        maskFile = kv.Key.ToString();
            //        maskStream = await kv.Value.Content.ReadAsStreamAsync(cancellationToken);
            //    }
            //    else if (imageFile == null)
            //    {
            //        imageFile = kv.Key.ToString();
            //        imageStream = await kv.Value.Content.ReadAsStreamAsync(cancellationToken);
            //    }
            //}
            //if (maskStream != null)
            //{
            //    cr = await imageClient.GenerateImageEditsAsync(
            //        imageStream, imageFile,
            //        prompt,
            //        maskStream, maskFile,
            //        options.MaxOutputTokenCount ?? 1,
            //        new ImageEditOptions()
            //        {
            //            EndUserId = options.EndUserId,
            //        }, cancellationToken);
            //}
            //else
            //{
            //    cr = await imageClient.GenerateImageEditsAsync(
            //        imageStream, imageFile,
            //        prompt,
            //        options.MaxOutputTokenCount ?? 1,
            //        new ImageEditOptions()
            //        {
            //            EndUserId = options.EndUserId,
            //        }, cancellationToken);
            //}

            MultiPartFormDataBinaryContent form = new();
            Dictionary<Uri, HttpResponseMessage> downloadedFiles = (await Task.WhenAll(images
                .Where(x => x.ImageUri != null)
                .GroupBy(x => x.ImageUri).Select(async image =>
                {
                    HttpResponseMessage resp = await http.GetAsync(image.Key, cancellationToken);
                    return (url: image.Key, resp);
                })))
            .ToDictionary(k => k.url, v => v.resp);

            foreach (ChatMessageContentPart image in images)
            {
                if (image.ImageUri != null)
                {
                    HttpResponseMessage file = downloadedFiles[image.ImageUri];
                    string fileName = Path.GetFileName(image.ImageUri.LocalPath);
                    if (fileName.Contains("mask.png"))
                    {
                        form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "mask", fileName, file.Content.Headers.ContentType?.ToString());
                    }
                    else
                    {
                        form.Add(await file.Content.ReadAsStreamAsync(cancellationToken), "image[]", fileName, file.Content.Headers.ContentType?.ToString());
                    }
                }
                else
                {
                    form.Add(image.ImageBytes, "image[]", image.Filename ?? DBFileDef.MakeFileNameByContentType(image.ImageBytesMediaType), image.ImageBytesMediaType);
                }
            }
            form.Add(prompt, "prompt");
            form.Add(options.MaxOutputTokenCount ?? 1, "n");
            form.Add(Model.ApiModelId, "model");
            if (_imageSize != DBKnownImageSize.Default)
            {
                form.Add(_imageSize switch
                {
                    DBKnownImageSize.W1024xH1024 => "1024x1024",
                    DBKnownImageSize.W1536xH1024 => "1536x1024",
                    DBKnownImageSize.W1024xH1536 => "1024x1536",
                    _ => throw new ArgumentOutOfRangeException(nameof(_imageSize), _imageSize, null)
                }, "size");
            }
            else
            {
                if (prompt.Contains("3:2"))
                {
                    form.Add("1536x1024", "size");
                }
                else if (prompt.Contains("2:3"))
                {
                    form.Add("1024x1536", "size");
                }
            }
            
            if (options.EndUserId != null)
            {
                form.Add(options.EndUserId, "user");
            }
            form.Add("low", "moderation");
            if (_reasoningEffort != DBReasoningEffort.Default)
            {
                form.Add(_reasoningEffort switch
                {
                    DBReasoningEffort.Low => "low",
                    DBReasoningEffort.Medium => "medium",
                    DBReasoningEffort.High => "high",
                    _ => throw new ArgumentOutOfRangeException(nameof(_reasoningEffort), _reasoningEffort, null)
                }, "quality");
            }

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

        static string GetPrompt(IReadOnlyList<ChatMessage> messages)
        {
            UserChatMessage? userMessage = messages.OfType<UserChatMessage>().LastOrDefault();
            if (userMessage != null)
            {
                ChatMessageContentPart? textPart = userMessage.Content
                    .Where(x => x.Kind == ChatMessageContentPartKind.Text)
                    .LastOrDefault();
                if (textPart != null)
                {
                    return textPart.Text;
                }
            }
            throw new InvalidOperationException($"Unable to find a text part in the user message.");
        }

        static ChatMessageContentPart[] GetImages(IReadOnlyList<ChatMessage> messages)
        {
            // if user message contains image, we need to use all images in the message as input
            UserChatMessage? userChatMessage = messages.OfType<UserChatMessage>().LastOrDefault();
            if (userChatMessage != null)
            {
                ChatMessageContentPart[] userMessageImages = [.. userChatMessage.Content.Where(x => x.Kind == ChatMessageContentPartKind.Image)];
                if (userMessageImages.Length > 0)
                {
                    return userMessageImages;
                }
            }

            // otherwise, we need to use the last image in the message as input
            return [.. messages.SelectMany(x => x.Content)
                .Where(x => x.Kind == ChatMessageContentPartKind.Image)
                .Reverse()
                .Take(1)];
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "FromClientResult")]
    static extern GeneratedImageCollection FromClientResult(GeneratedImageCollection _, ClientResult result);

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        _reasoningEffort = reasoningEffort;
    }

    protected override void SetImageSize(ChatCompletionOptions options, DBKnownImageSize imageSize)
    {
        _imageSize = imageSize;
    }
}
