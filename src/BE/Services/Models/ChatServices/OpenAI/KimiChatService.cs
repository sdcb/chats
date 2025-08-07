using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class KimiChatService(Model model) : ChatCompletionService(model, new Uri("https://api.moonshot.cn/v1"));