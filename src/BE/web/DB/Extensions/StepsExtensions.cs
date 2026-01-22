using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB.Extensions;

namespace Chats.BE.DB.Extensions;

public static class StepsExtensions
{
    extension(IEnumerable<Step> steps)
    {
        internal Step? LastUserMessage => steps
            .Where(x => (DBChatRole)x.ChatRoleId == DBChatRole.User)
            .LastOrDefault();
    }
}