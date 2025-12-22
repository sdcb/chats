using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

// Re-export ChatTool, ChatResponseFormat, etc. for easier access from other namespaces
// DBFinishReason is defined in Chats.BE.Services.Models namespace

/// <summary>
/// Base type for chat tools.
/// </summary>
public abstract record ChatTool;

/// <summary>
/// Represents a function tool that follows the OpenAI tool schema.
/// </summary>
public sealed record FunctionTool : ChatTool
{
    public required string FunctionName { get; init; }
    public string? FunctionDescription { get; init; }
    public string? FunctionParameters { get; init; }
    public bool? FunctionSchemaIsStrict { get; init; }

    public static FunctionTool Create(string name, string? description = null, string? parameters = null)
    {
        return new FunctionTool
        {
            FunctionName = name,
            FunctionDescription = description,
            FunctionParameters = parameters
        };
    }

    public JsonObject ToChatCompletionToolCall()
    {
        JsonObject function = new()
        {
            ["name"] = FunctionName
        };

        if (FunctionDescription != null)
        {
            function["description"] = FunctionDescription;
        }

        if (FunctionParameters != null)
        {
            function["parameters"] = JsonNode.Parse(FunctionParameters);
        }

        JsonObject tool = new()
        {
            ["type"] = "function",
            ["function"] = function
        };

        if (FunctionSchemaIsStrict == true)
        {
            tool["strict"] = true;
        }

        return tool;
    }

    public JsonObject ToResponseToolCall()
    {
        JsonObject function = new()
        {
            ["type"] = "function",
            ["name"] = FunctionName
        };

        if (FunctionDescription != null)
        {
            function["description"] = FunctionDescription;
        }

        if (FunctionParameters != null)
        {
            function["parameters"] = JsonNode.Parse(FunctionParameters);
        }

        if (FunctionSchemaIsStrict == true)
        {
            function["strict"] = true;
        }

        return function;
    }
}

/// <summary>
/// Represents Anthropic built-in tools (e.g., web_search, bash).
/// </summary>
public sealed record AnthropicBuiltInTool : ChatTool
{
    public required string Name { get; init; }
    public required string Type { get; init; }

    public JsonObject ToJsonObject()
    {
        JsonObject result = new()
        {
            ["name"] = Name,
            ["type"] = Type
        };

        return result;
    }
}

/// <summary>
/// Represents the response format for a chat completion.
/// </summary>
public class ChatResponseFormat
{
    public string Type { get; init; } = "text";
    public JsonObject? JsonSchema { get; init; }

    public static ChatResponseFormat Text { get; } = new() { Type = "text" };
    public static ChatResponseFormat JsonObject { get; } = new() { Type = "json_object" };

    public static ChatResponseFormat CreateJsonSchema(string name, JsonObject schema, bool? strict = null)
    {
        JsonObject jsonSchema = new()
        {
            ["name"] = name,
            ["schema"] = schema
        };
        if (strict.HasValue)
        {
            jsonSchema["strict"] = strict.Value;
        }
        return new ChatResponseFormat
        {
            Type = "json_schema",
            JsonSchema = jsonSchema
        };
    }

    public JsonNode? ToJsonNode()
    {
        if (Type == "text")
        {
            return null; // text is default, no need to send
        }

        JsonObject result = new()
        {
            ["type"] = Type
        };

        if (Type == "json_schema" && JsonSchema != null)
        {
            result["json_schema"] = JsonNode.Parse(JsonSchema.ToJsonString());
        }

        return result;
    }

    /// <summary>
    /// Returns only the type object for Response API format
    /// </summary>
    public JsonObject ToJsonObject()
    {
        return new JsonObject
        {
            ["type"] = Type
        };
    }
}

/// <summary>
/// Represents a tool call from the model.
/// </summary>
public class ChatToolCall
{
    public required string Id { get; init; }
    public required string FunctionName { get; init; }
    public required string FunctionArguments { get; init; }

    public static ChatToolCall CreateFunctionToolCall(string id, string name, string arguments)
    {
        return new ChatToolCall
        {
            Id = id,
            FunctionName = name,
            FunctionArguments = arguments
        };
    }
}

/// <summary>
/// Represents a streaming tool call update.
/// </summary>
public class StreamingChatToolCallUpdate
{
    public int Index { get; init; }
    public string? ToolCallId { get; init; }
    public string? FunctionName { get; init; }
    public string? FunctionArgumentsUpdate { get; init; }
}

/// <summary>
/// Helper class for parsing finish reason from API response to DBFinishReason.
/// </summary>
public static class DBFinishReasonParser
{
    public static DBFinishReason? Parse(string? reason) => reason switch
    {
        "stop" => DBFinishReason.Stop,
        "length" => DBFinishReason.Length,
        "tool_calls" => DBFinishReason.ToolCalls,
        "content_filter" => DBFinishReason.ContentFilter,
        "function_call" => DBFinishReason.FunctionCall,
        null or "" => null,
        _ => null
    };
}
