using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using OpenAI.Chat;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.BE.Services.Models;

public record ChatRequest
{
    /// <summary>
    /// Chat messages in neutral format (independent of any third-party SDK or database model).
    /// </summary>
    public required IList<NeutralMessage> Messages { get; init; }

    /// <summary>
    /// Optional system message with cache control support.
    /// When set, this takes precedence over ChatConfig.SystemPrompt.
    /// </summary>
    public NeutralSystemMessage? System { get; init; }

    public required ChatConfig ChatConfig { get; init; }

    public string? EndUserId { get; init; }

    public IList<ChatTool> Tools { get; init; } = [];

    public ChatResponseFormat? TextFormat { get; init; }

    public bool? AllowParallelToolCalls { get; init; }

    public bool Streamed { get; init; } = true;

    public float? TopP { get; init; }

    public long? Seed { get; init; }

    /// <summary>
    /// Gets the effective system prompt, prioritizing System over ChatConfig.SystemPrompt.
    /// </summary>
    public string? GetEffectiveSystemPrompt()
    {
        return System?.GetCombinedText() ?? ChatConfig.SystemPrompt;
    }

    public int EstimatePromptTokens(Tokenizer tokenizer)
    {
        const int TokenPerConversation = 3;
        const int TokenPerMessage = 4;
        int systemTokens = 0;
        string? systemPrompt = GetEffectiveSystemPrompt();
        if (systemPrompt != null)
        {
            systemTokens = TokenPerMessage + tokenizer.CountTokens(systemPrompt);
        }

        int messagesTokens = Messages.Select(m => EstimateMessageTokens(m, tokenizer) + TokenPerMessage).Sum();
        int totalTokens = TokenPerConversation + systemTokens + messagesTokens;
        return totalTokens;
    }

    private static int EstimateMessageTokens(NeutralMessage message, Tokenizer tokenizer)
    {
        const int TokenPerToolCall = 3;
        const int TokensPerImage = 1105; // https://platform.openai.com/docs/guides/vision/calculating-costs

        int tokens = 0;
        foreach (NeutralContent content in message.Contents)
        {
            tokens += content switch
            {
                NeutralTextContent text => tokenizer.CountTokens(text.Content),
                NeutralErrorContent error => tokenizer.CountTokens(error.Content),
                NeutralThinkContent think => tokenizer.CountTokens(think.Content),
                NeutralFileUrlContent or NeutralFileBlobContent or NeutralFileContent => TokensPerImage,
                NeutralToolCallContent toolCall => tokenizer.CountTokens(toolCall.Id) + tokenizer.CountTokens(toolCall.Name) + tokenizer.CountTokens(toolCall.Parameters) + TokenPerToolCall,
                NeutralToolCallResponseContent toolResp => tokenizer.CountTokens(toolResp.Response),
                _ => 0
            };
        }
        return tokens;
    }

    public static ChatRequest Simple(string prompt, Model model)
    {
        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText(prompt)],
            ChatConfig = new ChatConfig()
            {
                Model = model,
            }
        };
    }

    public static ChatRequest FromOpenAI(string userId, Model model, bool streamed, IList<ChatMessage> messages, ChatCompletionOptions cco)
    {
        ChatConfig config = new()
        {
            Model = model,
            ModelId = model.Id,
            SystemPrompt = messages.ExtractSystemPrompt(),
            Temperature = cco.Temperature,
            MaxOutputTokens = cco.MaxOutputTokenCount ?? cco._deprecatedMaxTokens,
            ReasoningEffortId = (byte)cco.ReasoningEffortLevel.ToDBReasoningEffort(),
        };

        IList<NeutralMessage> neutralMessages = messages.ToNeutralExcludingSystem();

        if (cco.Patch.TryGetValue("$.enable_search"u8, out bool enableSearch))
        {
            config.WebSearchEnabled = enableSearch;
        }
        if (cco.Patch.TryGetValue("$.image_size"u8, out string? imageSize))
        {
            config.ImageSize = imageSize;
        }
        if (cco.Patch.TryGetValue("$.enable_code_execution"u8, out bool enableCodeExecution))
        {
            config.CodeExecutionEnabled = enableCodeExecution;
        }

        return new ChatRequest()
        {
            ChatConfig = config,
            AllowParallelToolCalls = cco.AllowParallelToolCalls,
            EndUserId = userId,
            Streamed = streamed,
            TextFormat = cco.ResponseFormat,
            Tools = cco.Tools,
            Messages = neutralMessages,
            TopP = cco.TopP,
            Seed = cco.Seed,
        };
    }
}
