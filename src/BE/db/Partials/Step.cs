using Chats.DB.Enums;

namespace Chats.DB;

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

    public DBChatRole ChatRole => (DBChatRole)ChatRoleId;
}
