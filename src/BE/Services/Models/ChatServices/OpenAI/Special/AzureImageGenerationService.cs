using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationService(Model model) : ImageGenerationService(model, AzureChatCompletionService.HostTransform(model.ModelKey));
