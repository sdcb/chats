using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using Model = Chats.BE.DB.Model;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

//public class AnthropicChatService(Model model) : ChatService(model)
//{
//    public override IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
//    {
//        AnthropicClient anthropicClient = new(new ClientOptions()
//        {
//            BaseUrl = new Uri(Model.ModelKey.Host ?? "https://api.anthropic.com/"),
//            APIKey = Model.ModelKey.Secret,
//        });

//        MessageCreateParams message = new()
//        {
//            MaxTokens = Model.MaxResponseTokens,
//            Model = Model.DeploymentName,
//            Messages = [.. messages.Where(x => x is not SystemChatMessage).Select(ToAnthropicMessage)],
//            System = messages.OfType<SystemChatMessage>().FirstOrDefault() switch
//            {
//                null => null,
//                var x => new SystemModel(x.Content[0].Text)
//            }
//        };
//    }

//    MessageParam ToAnthropicMessage(ChatMessage chatMessage)
//    {
        
//    }
//}
