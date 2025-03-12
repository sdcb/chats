using Chats.BE.DB;
using Chats.BE.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record UpdateChatSpanRequest
{
    [JsonPropertyName("modelId")]
    public short? ModelId { get; init; }

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; init; }

    [JsonPropertyName("setsTemperature")]
    public bool SetsTemperature { get; init; } = false;

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    [JsonPropertyName("enableSearch")]
    public bool? WebSearchEnabled { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoningEffort")]
    public DBReasoningEffort? ReasoningEffort { get; init; }

    public async Task ApplyTo(ChatSpan span)
    {
        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatSpan.ChatConfig is null");

        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            config.SystemPrompt = SystemPrompt;
        }

        if (ModelId != null)
        {
            config.ModelId = ModelId.Value;
        }

        if (SetsTemperature)
        {
            config.Temperature = Temperature;
        }

        config.WebSearchEnabled = WebSearchEnabled ?? false;
        config.MaxOutputTokens = MaxOutputTokens;
        config.ReasoningEffort = (byte?)ReasoningEffort;
    }
}
