using Chats.BE.DB;
using Chats.BE.DB.Enums;
using OpenAI.Chat;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models;

public class AnthropicRequestWrapper(JsonObject json)
{
    public bool Streamed => (bool?)json["stream"] ?? false;

    public string? Model => (string?)json["model"];

    public int? MaxTokens => (int?)json["max_tokens"];

    public string? System => ParseSystemPrompt();

    public float? Temperature => (float?)json["temperature"];

    public float? TopP => (float?)json["top_p"];

    public int? TopK => (int?)json["top_k"];

    public JsonNode? Messages => json["messages"];

    public JsonNode? Tools => json["tools"];

    public JsonNode? Thinking => json["thinking"];

    public bool SeemsValid()
    {
        return Model != null && MaxTokens != null && Messages != null;
    }

    public ChatRequest ToChatRequest(string userId, Model model)
    {
        // Parse thinking config
        int? thinkingBudget = null;
        if (Thinking != null)
        {
            string? thinkingType = (string?)Thinking["type"];
            if (thinkingType == "enabled")
            {
                thinkingBudget = (int?)Thinking["budget_tokens"];
            }
        }

        ChatConfig config = new()
        {
            Model = model,
            ModelId = model.Id,
            SystemPrompt = System,
            Temperature = Temperature,
            MaxOutputTokens = MaxTokens,
            ThinkingBudget = thinkingBudget,
            WebSearchEnabled = false,
        };

        List<Step> steps = ParseMessages();
        List<ChatTool> tools = ParseTools();

        return new ChatRequest
        {
            ChatConfig = config,
            EndUserId = userId,
            Streamed = Streamed,
            Steps = steps,
            Tools = tools,
            TopP = TopP,
        };
    }

    private List<Step> ParseMessages()
    {
        List<Step> steps = [];

        if (Messages == null) return steps;

        foreach (JsonNode? messageNode in Messages.AsArray())
        {
            if (messageNode == null) continue;

            string? role = (string?)messageNode["role"];
            JsonNode? content = messageNode["content"];

            if (role == null || content == null) continue;

            DBChatRole chatRole = role switch
            {
                "user" => DBChatRole.User,
                "assistant" => DBChatRole.Assistant,
                _ => throw new ArgumentException($"Unknown role: {role}")
            };

            List<StepContent> stepContents = ParseContent(content, chatRole);

            // Handle tool results in user messages - they should be separate steps
            if (chatRole == DBChatRole.User)
            {
                List<StepContent> userContents = [];
                List<StepContent> toolResults = [];

                foreach (StepContent sc in stepContents)
                {
                    if (sc.ContentType == DBStepContentType.ToolCallResponse)
                    {
                        toolResults.Add(sc);
                    }
                    else
                    {
                        userContents.Add(sc);
                    }
                }

                // Add tool results as ToolCall role steps
                if (toolResults.Count > 0)
                {
                    steps.Add(new Step
                    {
                        ChatRoleId = (byte)DBChatRole.ToolCall,
                        StepContents = toolResults
                    });
                }

                // Add user content if any
                if (userContents.Count > 0)
                {
                    steps.Add(new Step
                    {
                        ChatRoleId = (byte)DBChatRole.User,
                        StepContents = userContents
                    });
                }
            }
            else
            {
                steps.Add(new Step
                {
                    ChatRoleId = (byte)chatRole,
                    StepContents = stepContents
                });
            }
        }

        return steps;
    }

    private static List<StepContent> ParseContent(JsonNode content, DBChatRole role)
    {
        List<StepContent> stepContents = [];

        // Content can be a string or an array
        if (content is JsonValue stringValue && stringValue.TryGetValue<string>(out string? text))
        {
            if (!string.IsNullOrEmpty(text))
            {
                stepContents.Add(StepContent.FromText(text));
            }
        }
        else if (content is JsonArray contentArray)
        {
            foreach (JsonNode? block in contentArray)
            {
                if (block == null) continue;

                string? type = (string?)block["type"];

                switch (type)
                {
                    case "text":
                        string? blockText = (string?)block["text"];
                        if (!string.IsNullOrEmpty(blockText))
                        {
                            stepContents.Add(StepContent.FromText(blockText));
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
                                    stepContents.Add(StepContent.FromFileBlob(imageBytes, mediaType));
                                }
                            }
                            else if (sourceType == "url")
                            {
                                string? url = (string?)source["url"];
                                if (!string.IsNullOrEmpty(url))
                                {
                                    stepContents.Add(StepContent.FromFileUrl(url));
                                }
                            }
                        }
                        break;

                    case "tool_use":
                        // Assistant requesting tool use
                        string? toolId = (string?)block["id"];
                        string? toolName = (string?)block["name"];
                        JsonNode? input = block["input"];
                        if (!string.IsNullOrEmpty(toolId) && !string.IsNullOrEmpty(toolName))
                        {
                            string inputJson = input?.ToJsonString() ?? "{}";
                            stepContents.Add(StepContent.FromTool(toolId, toolName, inputJson));
                        }
                        break;

                    case "tool_result":
                        // User providing tool result
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
                            stepContents.Add(StepContent.FromToolResponse(toolUseId, response, 0, !isError));
                        }
                        break;

                    case "thinking":
                        // Thinking content from previous assistant turn
                        string? thinking = (string?)block["thinking"];
                        string? signature = (string?)block["signature"];
                        if (!string.IsNullOrEmpty(thinking))
                        {
                            byte[]? signatureBytes = !string.IsNullOrEmpty(signature) ? Convert.FromBase64String(signature) : null;
                            stepContents.Add(StepContent.FromThink(thinking, signatureBytes));
                        }
                        break;

                    case "redacted_thinking":
                        // Redacted thinking - just signature
                        string? redactedSig = (string?)block["data"];
                        if (!string.IsNullOrEmpty(redactedSig))
                        {
                            stepContents.Add(StepContent.FromThink("", Convert.FromBase64String(redactedSig)));
                        }
                        break;
                }
            }
        }

        return stepContents;
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

    /// <summary>
    /// Parses system prompt which can be a string or an array of content blocks
    /// </summary>
    private string? ParseSystemPrompt()
    {
        JsonNode? systemNode = json["system"];
        if (systemNode == null) return null;

        // Case 1: Simple string
        if (systemNode is JsonValue stringValue && stringValue.TryGetValue<string>(out string? text))
        {
            return text;
        }

        // Case 2: Array of content blocks [{"type": "text", "text": "..."}]
        if (systemNode is JsonArray systemArray)
        {
            List<string> parts = [];
            foreach (JsonNode? block in systemArray)
            {
                if (block == null) continue;
                string? type = (string?)block["type"];
                if (type == "text")
                {
                    string? blockText = (string?)block["text"];
                    if (!string.IsNullOrEmpty(blockText))
                    {
                        parts.Add(blockText);
                    }
                }
            }
            return parts.Count > 0 ? string.Join("\n\n", parts) : null;
        }

        return null;
    }

    private List<ChatTool> ParseTools()
    {
        List<ChatTool> tools = [];

        if (Tools == null) return tools;

        foreach (JsonNode? toolNode in Tools.AsArray())
        {
            if (toolNode == null) continue;

            string? name = (string?)toolNode["name"];
            string? description = (string?)toolNode["description"];
            JsonNode? inputSchema = toolNode["input_schema"];

            if (string.IsNullOrEmpty(name)) continue;

            tools.Add(ChatTool.CreateFunctionTool(
                name,
                description,
                inputSchema != null ? BinaryData.FromString(inputSchema.ToJsonString()) : BinaryData.FromString("{}")
            ));
        }

        return tools;
    }

    public string Serialize()
    {
        return json.ToJsonString(JSON.JsonSerializerOptions);
    }
}
