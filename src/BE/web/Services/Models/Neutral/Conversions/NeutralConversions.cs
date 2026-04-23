using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.Neutral;

/// <summary>
/// Conversion methods for parsing OpenAI JSON format to NeutralMessage.
/// </summary>
public static class NeutralConversions
{
    /// <summary>
    /// Parses OpenAI format messages JSON array to a list of NeutralMessages.
    /// Excludes system messages (they should be handled separately).
    /// </summary>
    public static IList<NeutralMessage> ParseOpenAIMessages(JsonArray? messages)
    {
        if (messages == null) return [];

        List<NeutralMessage> result = [];
        foreach (JsonNode? msgNode in messages)
        {
            if (msgNode == null) continue;

            string? role = (string?)msgNode["role"];
            if (role == "system" || role == "developer") continue; // Skip system messages

            NeutralMessage? message = ParseSingleMessage(msgNode, role);
            if (message != null)
            {
                result.Add(message);
            }
        }
        return EnsureToolResponseIntegrity(result);
    }

    /// <summary>
    /// Ensures all tool_calls in assistant messages have corresponding tool response messages.
    /// Adds empty tool responses for any missing tool_call_ids to prevent upstream API errors.
    /// This handles cases where tool messages are silently dropped during parsing (e.g., missing tool_call_id).
    /// </summary>
    public static IList<NeutralMessage> EnsureToolResponseIntegrity(IList<NeutralMessage> messages)
    {
        if (messages.Count == 0) return messages;

        // Quick check: if no assistant messages with tool_calls, nothing to do
        bool hasToolCalls = false;
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role == NeutralChatRole.Assistant &&
                messages[i].Contents.Any(c => c is NeutralToolCallContent))
            {
                hasToolCalls = true;
                break;
            }
        }
        if (!hasToolCalls) return messages;

        List<NeutralMessage> result = new(messages.Count);
        bool modified = false;

        for (int i = 0; i < messages.Count; i++)
        {
            result.Add(messages[i]);

            if (messages[i].Role != NeutralChatRole.Assistant) continue;

            List<NeutralToolCallContent> toolCalls = messages[i].Contents.OfType<NeutralToolCallContent>().ToList();
            if (toolCalls.Count == 0) continue;

            // Collect tool_call_ids from immediately following tool messages
            HashSet<string> respondedIds = new();
            int j = i + 1;
            while (j < messages.Count && messages[j].Role == NeutralChatRole.Tool)
            {
                foreach (NeutralToolCallResponseContent resp in messages[j].Contents.OfType<NeutralToolCallResponseContent>())
                {
                    respondedIds.Add(resp.ToolCallId);
                }
                j++;
            }

            // Find missing tool_call_ids and add empty responses
            foreach (NeutralToolCallContent tc in toolCalls)
            {
                if (!respondedIds.Contains(tc.Id))
                {
                    modified = true;
                    result.Add(NeutralMessage.FromTool(
                        NeutralToolCallResponseContent.Create(tc.Id, "")));
                }
            }
        }

        return modified ? result : messages;
    }

    /// <summary>
    /// Extracts system prompt from OpenAI format messages JSON array.
    /// </summary>
    public static string? ExtractSystemPrompt(JsonArray? messages)
    {
        if (messages == null) return null;

        List<string> systemPrompts = [];
        foreach (JsonNode? msgNode in messages)
        {
            if (msgNode == null) continue;

            string? role = (string?)msgNode["role"];
            if (role != "system" && role != "developer") continue;

            JsonNode? content = msgNode["content"];
            if (content == null) continue;

            // Content can be string or array
            if (content is JsonValue jv && jv.TryGetValue(out string? text))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    systemPrompts.Add(text);
                }
            }
            else if (content is JsonArray arr)
            {
                foreach (JsonNode? part in arr)
                {
                    if (part == null) continue;
                    string? partText = (string?)part["text"];
                    if (!string.IsNullOrWhiteSpace(partText))
                    {
                        systemPrompts.Add(partText);
                    }
                }
            }
        }

        return systemPrompts.Count > 0 ? string.Join("\r\n", systemPrompts) : null;
    }

    private static NeutralMessage? ParseSingleMessage(JsonNode msgNode, string? role)
    {
        NeutralChatRole neutralRole = role switch
        {
            "user" => NeutralChatRole.User,
            "assistant" => NeutralChatRole.Assistant,
            "tool" => NeutralChatRole.Tool,
            _ => throw new NotSupportedException($"Role '{role}' is not supported.")
        };

        List<NeutralContent> contents = [];

        // Handle tool message specially
        if (role == "tool")
        {
            string? toolCallId = (string?)msgNode["tool_call_id"];
            string? response = GetContentAsString(msgNode["content"]);
            if (toolCallId != null)
            {
                contents.Add(NeutralToolCallResponseContent.Create(toolCallId, response ?? ""));
            }
        }
        else
        {
            // Parse reasoning_content for assistant messages (must come before content)
            // This enables interleaved thinking with tool calls, similar to DeepSeek
            if (role == "assistant")
            {
                JsonNode? reasoningNode = msgNode["reasoning_content"];
                if (reasoningNode != null)
                {
                    string? reasoningText = GetContentAsString(reasoningNode);
                    if (!string.IsNullOrEmpty(reasoningText))
                    {
                        contents.Add(NeutralThinkContent.Create(reasoningText));
                    }
                }
            }

            // Parse content
            JsonNode? contentNode = msgNode["content"];
            if (contentNode != null)
            {
                contents.AddRange(ParseContent(contentNode));
            }

            // Parse tool_calls for assistant messages
            JsonArray? toolCalls = msgNode["tool_calls"]?.AsArray();
            if (toolCalls != null && role == "assistant")
            {
                foreach (JsonNode? tc in toolCalls)
                {
                    if (tc == null) continue;

                    string? id = (string?)tc["id"];
                    JsonNode? function = tc["function"];
                    if (function == null || id == null) continue;

                    string? name = (string?)function["name"];
                    string? arguments = (string?)function["arguments"];

                    if (name != null)
                    {
                        contents.Add(NeutralToolCallContent.Create(id, name, arguments ?? "{}"));
                    }
                }
            }
        }

        if (contents.Count == 0) return null;

        return new NeutralMessage
        {
            Role = neutralRole,
            Contents = contents
        };
    }

    private static IEnumerable<NeutralContent> ParseContent(JsonNode contentNode)
    {
        // Content can be string or array
        if (contentNode is JsonValue jv && jv.TryGetValue(out string? text))
        {
            if (!string.IsNullOrEmpty(text))
            {
                yield return NeutralTextContent.Create(text);
            }
        }
        else if (contentNode is JsonArray arr)
        {
            foreach (JsonNode? part in arr)
            {
                if (part == null) continue;

                string? type = (string?)part["type"];
                switch (type)
                {
                    case "text":
                        string? partText = (string?)part["text"];
                        if (!string.IsNullOrEmpty(partText))
                        {
                            yield return NeutralTextContent.Create(partText);
                        }
                        break;

                    case "image_url":
                        JsonNode? imageUrl = part["image_url"];
                        string? url = (string?)imageUrl?["url"];
                        if (!string.IsNullOrEmpty(url))
                        {
                            // Check if it's a data URL (base64)
                            if (url.StartsWith("data:"))
                            {
                                int commaIndex = url.IndexOf(',');
                                if (commaIndex > 0)
                                {
                                    string header = url[5..commaIndex]; // Skip "data:"
                                    string base64Data = url[(commaIndex + 1)..];

                                    // Parse media type from header (e.g., "image/png;base64")
                                    string mediaType = header.Split(';')[0];
                                    byte[] data = Convert.FromBase64String(base64Data);
                                    yield return NeutralFileBlobContent.Create(data, mediaType);
                                }
                            }
                            else
                            {
                                yield return NeutralFileUrlContent.Create(url);
                            }
                        }
                        break;
                }
            }
        }
    }

    private static string? GetContentAsString(JsonNode? contentNode)
    {
        if (contentNode == null) return null;

        if (contentNode is JsonValue jv && jv.TryGetValue(out string? text))
        {
            return text;
        }

        if (contentNode is JsonArray arr)
        {
            List<string> texts = [];
            foreach (JsonNode? part in arr)
            {
                if (part == null) continue;
                string? partText = (string?)part["text"];
                if (!string.IsNullOrEmpty(partText))
                {
                    texts.Add(partText);
                }
            }
            return string.Join("", texts);
        }

        return null;
    }
}
