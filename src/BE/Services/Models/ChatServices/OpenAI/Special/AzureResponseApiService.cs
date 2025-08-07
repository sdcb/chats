using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureResponseApiService(Model model, ILogger logger) : ResponseApiService(model, logger, AzureChatCompletionService.HostTransform(model.ModelKey));