using System;
using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models;

public class AnthropicRequestWrapper(JsonObject json)
{
    public bool Streamed => (bool?)json["stream"] ?? false;

    public string? Model => (string?)json["model"];

    public int? MaxTokens => (int?)json["max_tokens"];

    public float? Temperature => (float?)json["temperature"];

    public float? TopP => (float?)json["top_p"];

    public int? TopK => (int?)json["top_k"];

    public JsonNode? MessagesNode => json["messages"];

    public JsonNode? SystemNode => json["system"];

    public JsonNode? Tools => json["tools"];

    public JsonNode? Thinking => json["thinking"];

    public bool SeemsValid()
    {
        return Model != null && MaxTokens != null && MessagesNode != null;
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

        // Parse system message with cache control support
        NeutralSystemMessage? systemMessage = AnthropicConversions.ParseAnthropicSystem(SystemNode);

        ChatConfig config = new()
        {
            Model = model,
            ModelId = model.Id,
            // Don't set SystemPrompt here - use the System property instead for cache control support
            SystemPrompt = null,
            Temperature = Temperature,
            MaxOutputTokens = MaxTokens,
            ThinkingBudget = thinkingBudget,
            WebSearchEnabled = false,
        };

        // Parse messages using neutral conversions
        IList<NeutralMessage> messages = AnthropicConversions.ParseAnthropicMessages(MessagesNode);
        List<ChatTool> tools = ParseTools();

        return new ChatRequest
        {
            ChatConfig = config,
            EndUserId = userId,
            Streamed = Streamed,
            Messages = messages,
            System = systemMessage,
            Tools = tools,
            TopP = TopP,
        };
    }

    private List<ChatTool> ParseTools()
    {
        List<ChatTool> tools = [];

        if (Tools == null) return tools;

        foreach (JsonNode? toolNode in Tools.AsArray())
        {
            if (toolNode == null) continue;

            string? name = (string?)toolNode["name"];
            string? type = (string?)toolNode["type"];
            string? description = (string?)toolNode["description"];
            JsonNode? inputSchema = toolNode["input_schema"];

            if (string.IsNullOrEmpty(name)) continue;

            if (string.IsNullOrEmpty(type) || type.Equals("function", StringComparison.OrdinalIgnoreCase))
            {
                tools.Add(FunctionTool.Create(
                    name,
                    description,
                    inputSchema?.ToJsonString()
                ));
            }
            else
            {
                tools.Add(new AnthropicBuiltInTool
                {
                    Name = name,
                    Type = type
                });
            }
        }

        return tools;
    }

    public string Serialize()
    {
        return json.ToJsonString(JSON.JsonSerializerOptions);
    }
}
