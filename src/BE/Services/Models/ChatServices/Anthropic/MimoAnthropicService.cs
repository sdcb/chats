using System.Text.Json.Nodes;
using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

/// <summary>
/// Xiaomi Mimo Anthropic-compatible messages service.
/// Enables thinking via `thinking: { type: "enabled" }` without budget_tokens.
/// </summary>
public class MimoAnthropicService(IHttpClientFactory httpClientFactory) : AnthropicChatService(httpClientFactory)
{
    protected override (string url, string apiKey) GetEndpointAndKey(ModelKey modelKey)
    {
        return (
            modelKey.Host ?? "https://api.xiaomimimo.com/anthropic",
            modelKey.Secret ?? throw new ArgumentNullException(nameof(modelKey), "ModelKey.Secret cannot be null for MimoAnthropicService")
        );
    }

    protected override JsonNode? BuildThinkingNode(ChatRequest request, bool allowThinking)
    {
        // Mimo enables thinking mode via `thinking: { type: "enabled" }` when ThinkingBudget is set.
        // Unlike standard Anthropic, Mimo doesn't support budget_tokens parameter.
        if (allowThinking && request.ChatConfig.ThinkingBudget.HasValue)
        {
            return new JsonObject
            {
                ["type"] = "enabled"
            };
        }
        return null;
    }
}
