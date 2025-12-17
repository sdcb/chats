using System.Text.Json.Nodes;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Xiaomi Mimo OpenAI-compatible chat completion service.
/// Enables interleaved thinking with tool calls by sending back reasoning_content.
/// </summary>
public class MimoChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override void AddAuthorizationHeader(HttpRequestMessage request, DB.ModelKey modelKey)
    {
        // Mimo uses api-key header instead of Authorization: Bearer
        request.Headers.Add("api-key", modelKey.Secret);
    }

    protected override bool TryBuildThinkingContentForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out string? thinkingContent)
    {
        // Mimo thinking mode tool calls require reasoning_content to be passed back.
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

        // Mimo enables thinking mode via `thinking: { type: "enabled" }` when ThinkingBudget is set.
        // Unlike other providers, Mimo doesn't support budget_tokens parameter.
        if (request.ChatConfig.ThinkingBudget.HasValue)
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "enabled"
            };
        }
        else
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "disabled"
            };
        }

        return body;
    }
}
