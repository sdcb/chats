using Chats.BE.DB;
using Chats.BE.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record UpdateChatSpanRequest
{
    [JsonPropertyName("modelId")]
    public short ModelId { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; init; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    [JsonPropertyName("webSearchEnabled")]
    public bool WebSearchEnabled { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoningEffort")]
    public DBReasoningEffort ReasoningEffort { get; init; }

    public void ApplyTo(ChatSpan span)
    {
        span.Enabled = span.Enabled;

        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatSpan.ChatConfig is null");
        config.ModelId = ModelId;
        config.SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt;
        config.Temperature = Temperature;
        config.WebSearchEnabled = WebSearchEnabled;
        config.MaxOutputTokens = MaxOutputTokens;
        config.ReasoningEffort = (byte)ReasoningEffort;
    }

    public void ApplyTo(ChatPresetSpan span, Model model)
    {
        if (model.Id != ModelId)
        {
            throw new ArgumentException("ModelId does not match the provided model", nameof(ModelId));
        }

        span.Enabled = span.Enabled;

        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatPresetSpan.ChatConfig is null");
        config.ModelId = ModelId;
        config.SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt;
        config.Temperature = Temperature;
        config.WebSearchEnabled = WebSearchEnabled;
        config.MaxOutputTokens = MaxOutputTokens;
        config.ReasoningEffort = (byte)ReasoningEffort;
    }

    public ChatPresetSpan ToDB(Model model)
    {
        if (model.Id != ModelId)
        {
            throw new ArgumentException("ModelId does not match the provided model", nameof(ModelId));
        }

        return new ChatPresetSpan()
        {
            Enabled = Enabled,
            ChatConfig = new ChatConfig()
            {
                ModelId = ModelId,
                Model = model,
                SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt,
                Temperature = Temperature,
                WebSearchEnabled = WebSearchEnabled,
                MaxOutputTokens = MaxOutputTokens,
                ReasoningEffort = (byte)ReasoningEffort,
            },
        };
    }
}
