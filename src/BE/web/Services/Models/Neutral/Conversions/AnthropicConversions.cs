using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.Neutral.Conversions;

/// <summary>
/// Conversion methods between Anthropic JSON messages and NeutralMessage.
/// </summary>
public static class AnthropicConversions
{
    /// <summary>
    /// Parses Anthropic messages JSON array to a list of NeutralMessages.
    /// </summary>
    public static IList<NeutralMessage> ParseAnthropicMessages(JsonNode? messagesNode)
    {
        List<NeutralMessage> messages = [];
        if (messagesNode == null) return messages;

        foreach (JsonNode? messageNode in messagesNode.AsArray())
        {
            if (messageNode == null) continue;

            string? role = (string?)messageNode["role"];
            JsonNode? content = messageNode["content"];

            if (role == null || content == null) continue;

            NeutralChatRole chatRole = role switch
            {
                "user" => NeutralChatRole.User,
                "assistant" => NeutralChatRole.Assistant,
                _ => throw new ArgumentException($"Unknown role: {role}")
            };

            List<NeutralContent> contents = ParseContent(content);

            // Handle tool results in user messages - they should be separate messages with Tool role
            if (chatRole == NeutralChatRole.User)
            {
                List<NeutralContent> userContents = [];
                List<NeutralContent> toolResults = [];

                foreach (NeutralContent c in contents)
                {
                    if (c is NeutralToolCallResponseContent)
                    {
                        toolResults.Add(c);
                    }
                    else
                    {
                        userContents.Add(c);
                    }
                }

                // Add tool results as Tool role messages
                if (toolResults.Count > 0)
                {
                    messages.Add(new NeutralMessage
                    {
                        Role = NeutralChatRole.Tool,
                        Contents = toolResults
                    });
                }

                // Add user content if any
                if (userContents.Count > 0)
                {
                    messages.Add(new NeutralMessage
                    {
                        Role = NeutralChatRole.User,
                        Contents = userContents
                    });
                }
            }
            else
            {
                messages.Add(new NeutralMessage
                {
                    Role = chatRole,
                    Contents = contents
                });
            }
        }

        return messages;
    }

    /// <summary>
    /// Parses Anthropic system prompt to a NeutralSystemMessage.
    /// </summary>
    public static NeutralSystemMessage? ParseAnthropicSystem(JsonNode? systemNode)
    {
        if (systemNode == null) return null;

        // Case 1: Simple string
        if (systemNode is JsonValue stringValue && stringValue.TryGetValue<string>(out string? text))
        {
            return NeutralSystemMessage.FromText(text);
        }

        // Case 2: Array of content blocks [{"type": "text", "text": "...", "cache_control": {...}}]
        if (systemNode is JsonArray systemArray)
        {
            List<NeutralSystemContent> contents = [];
            foreach (JsonNode? block in systemArray)
            {
                if (block == null) continue;
                string? type = (string?)block["type"];
                if (type == "text")
                {
                    string? blockText = (string?)block["text"];
                    if (!string.IsNullOrEmpty(blockText))
                    {
                        NeutralCacheControl? cacheControl = ParseCacheControl(block["cache_control"]);
                        contents.Add(new NeutralSystemContent
                        {
                            Text = blockText,
                            CacheControl = cacheControl
                        });
                    }
                }
            }
            return contents.Count > 0 ? new NeutralSystemMessage { Contents = contents } : null;
        }

        return null;
    }

    private static List<NeutralContent> ParseContent(JsonNode content)
    {
        List<NeutralContent> contents = [];

        // Content can be a string or an array
        if (content is JsonValue stringValue && stringValue.TryGetValue<string>(out string? text))
        {
            if (!string.IsNullOrEmpty(text))
            {
                contents.Add(NeutralTextContent.Create(text));
            }
        }
        else if (content is JsonArray contentArray)
        {
            foreach (JsonNode? block in contentArray)
            {
                if (block == null) continue;

                string? type = (string?)block["type"];
                NeutralCacheControl? cacheControl = ParseCacheControl(block["cache_control"]);

                switch (type)
                {
                    case "text":
                        string? blockText = (string?)block["text"];
                        if (!string.IsNullOrEmpty(blockText))
                        {
                            contents.Add(NeutralTextContent.Create(blockText, cacheControl));
                        }
                        break;

                    case "image":
                        JsonNode? source = block["source"];
                        if (source != null)
                        {
                            string? sourceType = (string?)source["type"];
                            if (sourceType == "base64")
                            {
                                string? mediaType = (string?)source["media_type"] ?? "image/jpeg";
                                string? data = (string?)source["data"];
                                if (!string.IsNullOrEmpty(data))
                                {
                                    byte[] imageBytes = Convert.FromBase64String(data);
                                    contents.Add(NeutralFileBlobContent.Create(imageBytes, mediaType, cacheControl));
                                }
                            }
                            else if (sourceType == "url")
                            {
                                string? url = (string?)source["url"];
                                if (!string.IsNullOrEmpty(url))
                                {
                                    contents.Add(NeutralFileUrlContent.Create(url, cacheControl));
                                }
                            }
                        }
                        break;

                    case "tool_use":
                        string? toolId = (string?)block["id"];
                        string? toolName = (string?)block["name"];
                        JsonNode? input = block["input"];
                        if (!string.IsNullOrEmpty(toolId) && !string.IsNullOrEmpty(toolName))
                        {
                            string inputJson = input?.ToJsonString() ?? "{}";
                            contents.Add(NeutralToolCallContent.Create(toolId, toolName, inputJson, cacheControl));
                        }
                        break;

                    case "tool_result":
                        string? toolUseId = (string?)block["tool_use_id"];
                        bool isError = (bool?)block["is_error"] ?? false;
                        JsonNode? resultContent = block["content"];
                        if (!string.IsNullOrEmpty(toolUseId))
                        {
                            string? response = resultContent switch
                            {
                                JsonValue jv when jv.TryGetValue<string>(out string? s) => s,
                                JsonArray arr => string.Join("\n", arr.Select(ExtractResultText)),
                                _ => resultContent?.ToJsonString()
                            };
                            contents.Add(NeutralToolCallResponseContent.Create(toolUseId, response ?? "", !isError, 0, cacheControl));
                        }
                        break;

                    case "thinking":
                        string? thinking = (string?)block["thinking"];
                        string? signature = (string?)block["signature"];
                        if (!string.IsNullOrEmpty(thinking))
                        {
                            contents.Add(NeutralThinkContent.Create(thinking, signature, cacheControl));
                        }
                        break;

                    case "redacted_thinking":
                        string? redactedSig = (string?)block["data"];
                        if (!string.IsNullOrEmpty(redactedSig))
                        {
                            contents.Add(NeutralThinkContent.Create("", redactedSig, cacheControl));
                        }
                        break;
                }
            }
        }

        return contents;
    }

    private static NeutralCacheControl? ParseCacheControl(JsonNode? cacheControlNode)
    {
        if (cacheControlNode == null) return null;

        string? type = (string?)cacheControlNode["type"];
        if (string.IsNullOrEmpty(type)) return null;

        return new NeutralCacheControl { Type = type };
    }

    private static string? ExtractResultText(JsonNode? node)
    {
        if (node == null) return null;
        string? type = (string?)node["type"];
        if (type == "text")
        {
            return (string?)node["text"];
        }
        return node.ToJsonString();
    }
}
