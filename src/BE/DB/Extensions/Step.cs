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
            DBChatRole.Assistant => AddToolCalls(new AssistantChatMessage(await StepContents
                .Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text)
                .ToAsyncEnumerable()
                .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken))),
            DBChatRole.ToolCall => new ToolChatMessage(StepContents.First().StepContentToolCallResponse!.ToolCallId, StepContents.First().StepContentToolCallResponse!.Response),
            _ => throw new NotImplementedException()
        };

        AssistantChatMessage AddToolCalls(AssistantChatMessage assistantChatMessage)
        {
            foreach (StepContent content in StepContents)
            {
                if (content.ContentTypeId == (byte)DBMessageContentType.ToolCall && content.StepContentToolCall != null)
                {
                    assistantChatMessage.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall.ToolCallId,
                        content.StepContentToolCall.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters)));
                }
            }
            return assistantChatMessage;
        }
    }
}
