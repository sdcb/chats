﻿using Chats.BE.DB;

namespace Chats.BE.Services.ChatServices.Implementations.OpenAI;

public class GLMChatService(Model model) : OpenAIChatService(model, new Uri("https://open.bigmodel.cn/api/paas/v4/"))
{
}