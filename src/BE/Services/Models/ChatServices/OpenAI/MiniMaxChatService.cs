using System.Text.Json.Nodes;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// MiniMax OpenAI-compatible chat completion service.
/// Supports interleaved thinking compatible format by using `reasoning_split=true`.
/// When tool calls are present, MiniMax requires returning `reasoning_details` in message history.
/// </summary>
public class MiniMaxChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override string ReasoningContentPropertyName => "reasoning_details";

    protected override bool TryCreateThinkingSegmentForStorage(string thinkingContent, string? thinkingSignature, out ChatSegment? segment)
    {
        if (string.IsNullOrEmpty(thinkingContent))
        {
            segment = null;
            return false;
        }

        segment = new ThinkChatSegment
        {
            Think = thinkingContent,
            Signature = thinkingSignature
        };
        return true;
    }

    protected override bool TryBuildThinkingNodeForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out JsonNode? thinkingNode)
    {
        // MiniMax interleaved thinking compatible format requires preserving `reasoning_details`.
        // Only attach it for assistant messages that contain tool calls.
        if (message.Role != NeutralChatRole.Assistant || toolCalls.Count == 0 || thinkingContents.Count == 0)
        {
            thinkingNode = null;
            return false;
        }

        // If we have preserved structured payloads (stored in Signature), pass them back as-is.
        JsonArray preserved = [];
        foreach (NeutralThinkContent think in thinkingContents)
        {
            if (string.IsNullOrWhiteSpace(think.Signature))
            {
                continue;
            }

            try
            {
                JsonNode? node = JsonNode.Parse(think.Signature);
                switch (node)
                {
                    case JsonArray arr:
                        foreach (JsonNode? item in arr)
                        {
                            if (item != null) preserved.Add(item);
                        }
                        break;
                    case JsonObject obj:
                        preserved.Add(obj);
                        break;
                }
            }
            catch
            {
                // Ignore malformed signature; fall back to minimal format below.
            }
        }

        if (preserved.Count > 0)
        {
            thinkingNode = preserved;
            return true;
        }

        string combined = string.Join("", thinkingContents.Select(t => t.Content));
        if (string.IsNullOrEmpty(combined))
        {
            thinkingNode = null;
            return false;
        }

        thinkingNode = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "reasoning.text",
                ["text"] = combined,
                ["index"] = 0,
                ["format"] = "MiniMax-response-v1",
            }
        };
        return true;
    }

    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        // For interleaved thinking compatible format, requests must include reasoning_split=true.
        body["reasoning_split"] = true;

        return body;
    }

    protected override JsonObject ToOpenAIMessage(NeutralMessage message)
    {
        JsonObject msg = base.ToOpenAIMessage(message);

        // MiniMax examples always include `content` in assistant tool-call messages.
        // Ensure it's present (can be empty) to maximize compatibility.
        if (message.Role == NeutralChatRole.Assistant && msg["tool_calls"] != null && msg["content"] == null)
        {
            msg["content"] = "";
        }

        return msg;
    }
}
