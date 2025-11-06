using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class LingYiChatService(Model model) : ChatCompletionService(model, new Uri("https://api.lingyiwanwu.com/v1"), 
    new ReplaceSseContentPolicy("\"finish_reason\":\"\"", "\"finish_reason\":null"))
{
}
