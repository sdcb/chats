using Chats.Web.Services.Models;

namespace Chats.Web.DB;

public static class StepExtensions
{
    extension(IEnumerable<Step> steps)
    {
        internal Step? LastUserMessage => steps
            .Where(x => (DBChatRole)x.ChatRoleId == DBChatRole.User)
            .LastOrDefault();
    }
}