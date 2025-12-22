namespace Chats.Web.Services.Models;

public enum DBChatRole
{
    //System = 1, // System not used because moved to ChatSpan -> ChatConfig
    User = 2,
    Assistant = 3,
    ToolCall = 4,
}
