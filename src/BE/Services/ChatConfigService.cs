using Chats.BE.DB;

namespace Chats.BE.Services;

public class ChatConfigService(ChatsDB db)
{
    public async Task<ChatConfig> GetOrCreateChatConfig(ChatConfig raw)
    {
        throw new NotImplementedException();
    }
}
