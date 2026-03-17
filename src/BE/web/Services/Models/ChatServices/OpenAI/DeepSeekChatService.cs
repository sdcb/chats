using System.Text.Json.Nodes;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// DeepSeek OpenAI-compatible chat completion service.
/// Enables interleaved thinking with tool calls by sending back reasoning_content.
/// </summary>
public class DeepSeekChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override IEnumerable<JsonObject> ToOpenAIMessages(NeutralMessage message)
    {
        foreach (JsonObject upstreamMessage in base.ToOpenAIMessages(message))
        {
            yield return NormalizeToolMessageContent(upstreamMessage);
        }
    }

    protected override JsonObject ToOpenAIMessage(NeutralMessage message)
    {
        return NormalizeToolMessageContent(base.ToOpenAIMessage(message));
    }

    protected override bool TryBuildThinkingContentForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out string? thinkingContent)
    {
        // DeepSeek thinking mode tool calls require reasoning_content to be passed back.
        // Only attach it for assistant messages that contain tool calls.
        if (message.Role != NeutralChatRole.Assistant || toolCalls.Count == 0 || thinkingContents.Count == 0)
        {
            thinkingContent = null;
            return false;
        }

        thinkingContent = string.Join("", thinkingContents.Select(t => t.Content));
        return !string.IsNullOrEmpty(thinkingContent);
    }

    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        // DeepSeek enables thinking mode via `thinking: { type: "enabled" }`.
        // We map ChatConfig.ThinkingBudget presence to "enabled" (budget is provider-specific, so we don't send it).
        if (request.ChatConfig.ThinkingBudget.HasValue)
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "enabled"
            };
        }

        return body;
    }

    private static JsonObject NormalizeToolMessageContent(JsonObject upstreamMessage)
    {
        if ((string?)upstreamMessage["role"] != "tool" || upstreamMessage["content"] is not JsonArray contentArray)
        {
            return upstreamMessage;
        }

        List<string> textParts = [];
        foreach (JsonNode? node in contentArray)
        {
            if (node is not JsonObject part || !string.Equals((string?)part["type"], "text", StringComparison.Ordinal))
            {
                return upstreamMessage;
            }

            string? text = (string?)part["text"];
            if (!string.IsNullOrEmpty(text))
            {
                textParts.Add(text);
            }
        }

        upstreamMessage["content"] = textParts.Count switch
        {
            0 => string.Empty,
            1 => textParts[0],
            _ => string.Join("\n", textParts)
        };

        return upstreamMessage;
    }
}
