using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record ChatSpanRequest : CreateChatSpanRequest
{
    [JsonPropertyName("id")]
    public required byte Id { get; init; }

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; init; }

    public bool SystemPromptValid => !string.IsNullOrEmpty(SystemPrompt);

    public ChatCompletionOptions ToChatCompletionOptions(int userId, ChatSpan span)
    {
        ChatCompletionOptions cco = new()
        {
            Temperature = span.Temperature,
            EndUserId = userId.ToString(),
        };
        return cco;
    }
}

public record ChatRequest
{
    public required int ChatId { get; init; }

    public required ChatSpanRequest[] Spans { get; init; }

    public required long? MessageId { get; init; }

    public required MessageContentRequest? UserMessage { get; init; }

    public required short TimezoneOffset { get; init; }
}