using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.UserContext;
using Microsoft.ML.Tokenizers;

namespace Chats.BE.DB.Extensions;

public static class StepExtensions
{
    extension(Step step)
    {
        public int EstimatePromptTokens(Tokenizer tokenizer, byte? targetSpanId = null)
        {
            const int TokenPerToolCall = 3;
            return step.StepContents.Sum(c => (DBStepContentType)c.ContentTypeId switch
            {
                DBStepContentType.FileId => 1105, // https://platform.openai.com/docs/guides/vision/calculating-costs, assume image is ~2048x4096 in detail: high, mosts 1105 tokens
                DBStepContentType.Text => tokenizer.CountTokens(targetSpanId == null
                    ? c.StepContentText!.Content
                    : UserContextTemplate.Render(c.StepContentText!.ContextTemplate, c.StepContentText.Content, targetSpanId.Value)),
                DBStepContentType.Error => tokenizer.CountTokens(c.StepContentText!.Content),
                DBStepContentType.ToolCall => tokenizer.CountTokens(c.StepContentToolCall!.ToolCallId) + tokenizer.CountTokens(c.StepContentToolCall.Name) + tokenizer.CountTokens(c.StepContentToolCall.Parameters) + TokenPerToolCall,
                _ => 0
            });
        }
    }
}
