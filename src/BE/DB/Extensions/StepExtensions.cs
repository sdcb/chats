using Chats.BE.Services.Models;

namespace Chats.BE.DB;

public static class StepExtensions
{
    extension(IEnumerable<Step> steps)
    {
        internal Step? LastUserMessage => steps
            .Where(x => (DBChatRole)x.ChatRoleId == DBChatRole.User)
            .LastOrDefault();
    }
}