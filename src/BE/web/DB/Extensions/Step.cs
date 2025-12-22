using Chats.Web.DB.Enums;
using Chats.Web.Services.Models;
using Microsoft.ML.Tokenizers;

namespace Chats.Web.DB;

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

    public DBChatRole ChatRole => (DBChatRole)ChatRoleId;
}
