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
            AssistantChatMessage msg = null!;

            bool hasStuff = stepContents.Any(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text);

            if (hasStuff)
            {
                msg = new(await stepContents
                    .Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text)
                    .ToAsyncEnumerable()
                    .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                    .ToArrayAsync(cancellationToken));
            }
            else
            {
                msg = new AssistantChatMessage("");
            }

            foreach (StepContent content in stepContents)
            {
                if (content.ContentTypeId == (byte)DBMessageContentType.ToolCall && content.StepContentToolCall != null)
                {
                    msg.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall.ToolCallId,
                        content.StepContentToolCall.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters)));
                }
            }
            return msg;
        }
    }
}
