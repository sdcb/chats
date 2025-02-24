using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(Model model) : OpenAIChatService(model, new Uri("https://api.hunyuan.cloud.tencent.com/v1"));