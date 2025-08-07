using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class OllamaChatService(Model model) : ChatCompletionService(model);