using Chats.BE.Services.Models.Neutral;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Xiaomi Mimo OpenAI-compatible chat completion service.
/// Enables interleaved thinking with tool calls by sending back reasoning_content.
/// </summary>
public class MimoChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);
        if (!request.ChatConfig.Model.CurrentSnapshot.AllowSearch || !request.ChatConfig.WebSearchEnabled)
        {
            return body;
        }

        JsonArray tools = body["tools"] as JsonArray ?? [];
        if (body["tools"] == null)
        {
            body["tools"] = tools;
        }
        tools.Add(new JsonObject { ["type"] = "web_search" });
        body["tool_choice"] = "auto";
        return body;
    }

    protected override JsonArray ParseHostedWebSearchAnnotations(JsonElement annotations)
    {
        JsonArray results = [];
        HashSet<string> urls = new(StringComparer.Ordinal);
        if (annotations.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (JsonElement annotation in annotations.EnumerateArray())
        {
            if (!annotation.TryGetProperty("type", out JsonElement typeEl)
                || typeEl.GetString() != "url_citation")
            {
                continue;
            }

            string? url = GetString(annotation, "url");
            if (!string.IsNullOrEmpty(url) && !urls.Add(url))
            {
                continue;
            }

            JsonObject result = new()
            {
                ["type"] = "web_search_result",
                ["title"] = GetString(annotation, "title"),
                ["url"] = url,
                ["page_age"] = null,
                ["summary"] = GetString(annotation, "summary"),
                ["site_name"] = GetString(annotation, "site_name"),
                ["publish_time"] = GetString(annotation, "publish_time"),
                ["logo_url"] = GetString(annotation, "logo_url"),
            };
            results.Add(result);
        }
        return results;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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
}
