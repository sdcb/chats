using Chats.Web.Controllers.Users.Usages.Dtos;
using Chats.Web.DB;
using Chats.Web.Services.Models.ChatServices.OpenAI;
using Chats.Web.Services.Models.Neutral;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.Web.Services.Models;

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

    public required UsageSource Source { get; init; }

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

    public static ChatRequest SimpleValidate(string prompt, Model model)
    {
        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText(prompt)],
            ChatConfig = new ChatConfig()
            {
                Model = model,
            },
            Source = UsageSource.Validate,
        };
    }

    /// <summary>
    /// Creates a ChatRequest from OpenAI compatible API input
    /// </summary>
    public static ChatRequest FromOpenAI(string endUserId, Model model, bool streamed, IList<NeutralMessage> messages, CcoWrapper cco)
    {
        return new ChatRequest
        {
            EndUserId = endUserId,
            Messages = messages,
            Streamed = streamed,
            Tools = cco.Tools,
            TextFormat = cco.ResponseFormat,
            AllowParallelToolCalls = cco.AllowParallelToolCalls,
            TopP = cco.TopP,
            Seed = cco.Seed,
            Source = UsageSource.Api,
            ChatConfig = new ChatConfig
            {
                Model = model,
                Temperature = cco.Temperature,
                MaxOutputTokens = cco.MaxOutputTokens,
                WebSearchEnabled = cco.EnableSearch ?? false,
                CodeExecutionEnabled = cco.EnableCodeExecution ?? false,
                SystemPrompt = cco.SystemPrompt,
                ImageSize = cco.ImageSize,
                ReasoningEffortId = (byte)DB.Enums.DBReasoningEffortExtensions.FromString(cco.ReasoningEffort),
            }
        };
    }
}
