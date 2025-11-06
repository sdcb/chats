using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class DoubaoChatService(Model model) : ChatCompletionService(model, new Uri("https://ark.cn-beijing.volces.com/api/v3/"))
{
}