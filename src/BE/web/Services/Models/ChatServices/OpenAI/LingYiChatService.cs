namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// LingYi (01.ai) Chat Service
/// Note: Original implementation had SSE content replacement for finish_reason.
/// The new HttpClient-based implementation handles empty/null finish_reason gracefully.
/// </summary>
public class LingYiChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
}
