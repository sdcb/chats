namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// LiteLLM Chat Service.
/// LiteLLM Proxy exposes an OpenAI-compatible Chat Completions API and a
/// standard /models endpoint, so the base OpenAI-compatible transport and
/// model auto-discovery are reused as-is. This first-class provider ships a
/// sensible default host (http://localhost:4000/v1) so users can point Chats
/// at a LiteLLM gateway and reach 100+ upstream providers (OpenAI, Anthropic,
/// Bedrock, Vertex AI, Azure, etc.) through a single endpoint.
/// </summary>
public class LiteLLMChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
}
