using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class Step
{
    public ChatMessage ToOpenAI()
    {
        return (DBChatRole)ChatRoleId switch
        {
            DBChatRole.User => new UserChatMessage([.. StepContents.Select(c => c.ToTempOpenAI())]),
            DBChatRole.Assistant => ToAssistantMessage(StepContents),
            DBChatRole.ToolCall => new ToolChatMessage(StepContents.First().StepContentToolCallResponse!.ToolCallId, StepContents.First().StepContentToolCallResponse!.Response),
            _ => throw new NotImplementedException()
        };

        static AssistantChatMessage ToAssistantMessage(ICollection<StepContent> stepContents)
        {
            bool hasContent = false, hasToolCall = false;
            foreach (StepContent stepContent in stepContents)
            {
                if (stepContent.ContentTypeId == (byte)DBMessageContentType.FileId || 
                    stepContent.ContentTypeId == (byte)DBMessageContentType.Text || 
                    stepContent.ContentTypeId == (byte)DBMessageContentType.Error)
                {
                    hasContent = true;
                }
                if (stepContent.ContentTypeId == (byte)DBMessageContentType.ToolCall && stepContent.StepContentToolCall != null)
                {
                    hasToolCall = true;
                }
                if (hasContent && hasToolCall)
                {
                    break; // No need to check further if both are found
                }
            }

            if (hasContent)
            {
                AssistantChatMessage msg = new(stepContents
                    .Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text or DBMessageContentType.Error)
                    .Select(c => c.ToTempOpenAI())
                    .ToArray());
                foreach (ChatToolCall toolCall in stepContents.Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.ToolCall && x.StepContentToolCall != null)
                    .Select(content => ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall!.ToolCallId,
                        content.StepContentToolCall!.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters))))
                {
                    msg.ToolCalls.Add(toolCall);
                }
                return msg;
            }
            else if (hasToolCall) // onlyToolCall
            {
                return new(stepContents.Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.ToolCall && x.StepContentToolCall != null)
                    .Select(content => ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall!.ToolCallId,
                        content.StepContentToolCall!.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters))));
            }
            else
            {
                throw new Exception("Assistant chat message must either have at least one content or tool call");
            }
        }
    }
}
