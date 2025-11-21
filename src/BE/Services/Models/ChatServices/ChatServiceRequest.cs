using Chats.BE.DB;
using Chats.BE.DB.Enums;
using OpenAI.Chat;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.BE.Services.Models;

public record ChatServiceRequest
{
    public required IList<Step> Steps { get; init; }

    public required ChatConfig ChatConfig { get; init; }

    public string? EndUserId { get; init; }

    public IList<ChatTool> Tools { get; init; } = [];

    public ChatResponseFormat? TextFormat { get; init; }

    public bool? AllowParallelToolCalls { get; init; }

    public bool Streamed { get; init; } = true;

    public float? TopP { get; init; }

    public long? Seed { get; init; }

    public int EstimatePromptTokens(Tokenizer tokenizer)
    {
        const int TokenPerConversation = 3;
        const int TokenPerMessage = 4;
        int systemTokens = 0;
        if (ChatConfig.SystemPrompt != null)
        {
            systemTokens = TokenPerMessage + tokenizer.CountTokens(ChatConfig.SystemPrompt);
        }

        int stepsTokens = Steps.Select(x => x.EstimatePromptTokens(tokenizer) + TokenPerMessage).Sum();
        int totalTokens = TokenPerConversation + systemTokens + stepsTokens;
        return totalTokens;
    }

    public static ChatServiceRequest Simple(string prompt)
    {
        return new ChatServiceRequest
        {
            Steps =
            [
                new Step()
                {
                    ChatRoleId = (byte)DBChatRole.User,
                    StepContents =
                    [
                        new StepContent()
                        {
                            StepContentText = new StepContentText()
                            {
                                Content = prompt
                            }
                        }
                    ]
                }
            ],
            ChatConfig = new ChatConfig(),
        };
    }

    public static ChatServiceRequest FromOpenAI(string userId, Model model, bool streamed, IList<ChatMessage> messages, ChatCompletionOptions cco)
    {
        ChatConfig config = new()
        {
            Model = model, 
            ModelId = model.Id,
            SystemPrompt = string.Join("\r\n", messages
                .Where(x => x is SystemChatMessage or DeveloperChatMessage)
                .Select(x => string.Join("\r\n", x.Content.Select(x => x.Text)))) switch
                {
                    var x when !string.IsNullOrWhiteSpace(x) => x,
                    _ => null
                },
            Temperature = cco.Temperature,
            MaxOutputTokens = cco.MaxOutputTokenCount ?? cco._deprecatedMaxTokens,
            ReasoningEffort = (byte)cco.ReasoningEffortLevel.ToDBReasoningEffort(),
        };
        List<Step> steps = [.. messages
            .Where(x => x is not SystemChatMessage)
            .Select(Step.FromOpenAI)];
        
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

        return new ChatServiceRequest()
        {
            ChatConfig = config,
            AllowParallelToolCalls = cco.AllowParallelToolCalls,
            EndUserId = userId,
            Streamed = streamed,
            TextFormat = cco.ResponseFormat,
            Tools = cco.Tools,
            Steps = steps,
            TopP = cco.TopP,
            Seed = cco.Seed,
        };
    }
}