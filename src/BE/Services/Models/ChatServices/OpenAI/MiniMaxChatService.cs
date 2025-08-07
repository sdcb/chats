using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class MiniMaxChatService(Model model) : ChatCompletionService(model, new Uri("https://api.minimax.chat/v1"));
