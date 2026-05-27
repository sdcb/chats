using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Xiaomi Mimo OpenAI-compatible chat completion service.
/// Enables interleaved thinking with tool calls by sending back reasoning_content.
/// </summary>
public class MimoChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
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
