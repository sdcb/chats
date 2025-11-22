using Chats.BE.DB.Enums;
using Chats.BE.Services.Models;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class Step
{
    public Step WithNoMessage()
    {
        return new Step
        {
            TurnId = TurnId,
            ChatRoleId = ChatRoleId,
            Edited = Edited,
            CreatedAt = CreatedAt,
            UsageId = UsageId,
            StepContents = new List<StepContent>(capacity: StepContents.Count),
        };
    }

    public int EstimatePromptTokens(Tokenizer tokenizer)
    {
        const int TokenPerToolCall = 3;
        return StepContents.Sum(c => (DBStepContentType)c.ContentTypeId switch
        {
            DBStepContentType.FileId => 1105, // https://platform.openai.com/docs/guides/vision/calculating-costs, assume image is ~2048x4096 in detail: high, mosts 1105 tokens
            DBStepContentType.Text or DBStepContentType.Error => tokenizer.CountTokens(c.StepContentText!.Content),
            DBStepContentType.ToolCall => tokenizer.CountTokens(c.StepContentToolCall!.ToolCallId) + tokenizer.CountTokens(c.StepContentToolCall.Name) + tokenizer.CountTokens(c.StepContentToolCall.Parameters) + TokenPerToolCall,
            _ => 0
        });
    }

    public static Step FromOpenAI(ChatMessage message)
    {
        return new Step
        {
            ChatRoleId = message switch
            {
                UserChatMessage => (byte)DBChatRole.User,
                AssistantChatMessage => (byte)DBChatRole.Assistant,
                ToolChatMessage => (byte)DBChatRole.ToolCall,
                _ => throw new NotSupportedException($"Chat message type {message.GetType().Name} is not supported.")
            },
            StepContents = StepContent.FromOpenAI(message),
        };
    }

    public DBChatRole ChatRole => (DBChatRole)ChatRoleId;
}
