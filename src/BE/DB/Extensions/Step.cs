using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class Step
{
    public async Task<ChatMessage> ToOpenAI(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return (DBChatRole)ChatRoleId switch
        {
            DBChatRole.User => new UserChatMessage(await StepContents
                .ToAsyncEnumerable()
                .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken)),
            DBChatRole.Assistant => await ToAssistantMessage(StepContents, fup, cancellationToken),
            DBChatRole.ToolCall => new ToolChatMessage(StepContents.First().StepContentToolCallResponse!.ToolCallId, StepContents.First().StepContentToolCallResponse!.Response),
            _ => throw new NotImplementedException()
        };

        static async Task<AssistantChatMessage> ToAssistantMessage(ICollection<StepContent> stepContents, FileUrlProvider fup, CancellationToken cancellationToken)
        {
            bool hasContent = false, hasToolCall = false;
            foreach (StepContent stepContent in stepContents)
            {
                if (stepContent.ContentTypeId == (byte)DBMessageContentType.FileId || stepContent.ContentTypeId == (byte)DBMessageContentType.Text)
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
                AssistantChatMessage msg = new(await stepContents
                    .Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text)
                    .ToAsyncEnumerable()
                    .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                    .ToArrayAsync(cancellationToken));
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
