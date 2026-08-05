using Chats.BE.Services.Models.ChatServices.OpenAI;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

internal static class DeepSeekHostedWebSearch
{
    public const string InternalToolName = "web_search_call";
    public const string UpstreamToolName = "web_search";
    public const string ToolType = "web_search_20250305";
    public const string ServerToolUseType = "server_tool_use";
    public const string ToolResultType = "web_search_tool_result";

    public static AnthropicBuiltInTool CreateTool() => new()
    {
        Name = UpstreamToolName,
        Type = ToolType,
        Definition = new JsonObject
        {
            ["type"] = ToolType,
            ["name"] = UpstreamToolName,
        },
    };

    public static bool IsToolDefinition(JsonNode? node)
    {
        return node is JsonObject obj
            && string.Equals(obj["type"]?.GetValue<string>(), ToolType, StringComparison.Ordinal);
    }

    public static bool TryParseBlock(string? json, string expectedType, out JsonObject? block)
    {
        block = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            block = JsonNode.Parse(json) as JsonObject;
            return block != null
                && string.Equals(block["type"]?.GetValue<string>(), expectedType, StringComparison.Ordinal);
        }
        catch
        {
            block = null;
            return false;
        }
    }

    public static bool TryCreatePresentationCall(string rawCall, out string presentationCall)
    {
        presentationCall = rawCall;
        if (!TryParseBlock(rawCall, ServerToolUseType, out JsonObject? block) || block == null)
        {
            return false;
        }

        JsonObject action = new() { ["type"] = "search" };
        if (block["input"] is JsonObject input)
        {
            foreach ((string name, JsonNode? value) in input)
            {
                action[name] = value?.DeepClone();
            }
        }

        presentationCall = new JsonObject
        {
            ["type"] = InternalToolName,
            ["status"] = "completed",
            ["action"] = action,
        }.ToJsonString(JSON.JsonSerializerOptions);
        return true;
    }

    public static bool TryCreatePresentationResponse(string rawResponse, out string presentationResponse)
    {
        presentationResponse = rawResponse;
        if (!TryParseBlock(rawResponse, ToolResultType, out JsonObject? block)
            || block?["content"] is not JsonArray content)
        {
            return false;
        }

        JsonArray sanitized = [];
        foreach (JsonNode? item in content)
        {
            if (item is not JsonObject obj)
            {
                sanitized.Add(item?.DeepClone());
                continue;
            }

            JsonObject clone = [];
            foreach ((string name, JsonNode? value) in obj)
            {
                if (!string.Equals(name, "encrypted_content", StringComparison.Ordinal))
                {
                    clone[name] = value?.DeepClone();
                }
            }
            sanitized.Add(clone);
        }
        presentationResponse = sanitized.ToJsonString(JSON.JsonSerializerOptions);
        return true;
    }
}
