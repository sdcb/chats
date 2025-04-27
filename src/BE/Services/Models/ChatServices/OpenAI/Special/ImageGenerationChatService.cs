using OpenAI;
using System.ClientModel;
using Chats.BE.DB;
using OpenAI.Images;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using Chats.BE.DB.Enums;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ImageGenerationChatService(Model model) : ChatService(model)
{
    private DBReasoningEffort _reasoningEffort;

    protected virtual ImageClient CreateImageGenerationAPI(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Host, nameof(model.ModelKey.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        OpenAIClient api = new OpenAIClient(
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
        ChatMessageContentPart? image = GetImage(messages);
        ImageClient ic = CreateImageGenerationAPI(Model);
        ClientResult<GeneratedImageCollection> cr = null!;
        if (image == null)
        {
            cr = await ic.GenerateImagesAsync(
                prompt,
                options.MaxOutputTokenCount ?? 4,
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
            cr = await ic.GenerateImageEditsAsync(
                await http.GetStreamAsync(image.ImageUri, cancellationToken), "input-image",
                prompt,
                options.MaxOutputTokenCount ?? 4,
                new ImageEditOptions()
                {
                    EndUserId = options.EndUserId,
                }, cancellationToken);
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

        static ChatMessageContentPart? GetImage(IReadOnlyList<ChatMessage> messages)
        {
            return messages.SelectMany(x => x.Content)
                .Where(x => x.Kind == ChatMessageContentPartKind.Image)
                .LastOrDefault();
        }
    }

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        _reasoningEffort = reasoningEffort;
    }
}
