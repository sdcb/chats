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

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ImageGenerationChatService(Model model) : ChatService(model)
{
    private DBReasoningEffort _reasoningEffort;

    protected virtual ImageClient CreateImageGenerationAPI(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Host, nameof(model.ModelKey.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        OpenAIClient api = new(
            new ApiKeyCredential(model.ModelKey.Secret), new()
            {
                NetworkTimeout = NetworkTimeout,
                Endpoint = model.ModelKey.Host != null ? new Uri(model.ModelKey.Host) : null,
            });
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
        ImageClient ic = CreateImageGenerationAPI(Model);
        ClientResult<GeneratedImageCollection> cr = null!;
        if (images.Length == 0)
        {
            cr = await ic.GenerateImagesAsync(
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
                }, cancellationToken);
        }
        else
        {
            using HttpClient http = new();
            //cr = await ic.GenerateImageEditsAsync(
            //    await http.GetStreamAsync(image.ImageUri, cancellationToken), Path.GetFileName(image.ImageUri.LocalPath),
            //    prompt,
            //    options.MaxOutputTokenCount ?? 1,
            //    new ImageEditOptions()
            //    {
            //        EndUserId = options.EndUserId,
            //    }, cancellationToken);
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
            form.Add(options.EndUserId, "user");
            //form.Add("low", "moderation");
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
            //multiPartFormDataBinaryContent.Add("1024x1024", "size");

            ClientResult clientResult = await ic.GenerateImageEditsAsync(form, form.ContentType, new RequestOptions()
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

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        _reasoningEffort = reasoningEffort;
    }
}
