using System.Text.Json.Nodes;
using Chats.BE.DB;
using Chats.BE.Services.Models.Neutral;

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

    protected override void AddApiKeyHeader(HttpRequestMessage request, string apiKey)
    {
        // Mimo uses api-key header instead of x-api-key
        request.Headers.Add("api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
    }

    /// <summary>
    /// Mimo requires all historical thinking blocks to be preserved for interleaved thinking.
    /// If thinking blocks are removed from previous turns, tool calls may be incorrectly
    /// output into reasoning_content instead of tool_calls field.
    /// </summary>
    protected override IList<NeutralMessage> RemoveNonCurrentTurnThinkingBlocks(IList<NeutralMessage> messages)
    {
        // Do not remove any thinking blocks - Mimo needs them all
        return messages;
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
