using Aliyun.OSS;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Buffers.Text;
using System.Text.Json;
using Model = Chats.BE.DB.Model;

//namespace Chats.BE.Services.Models.ChatServices.Anthropic;

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
//            Messages = ToAnthropicMessages(messages.Where(x => x is not SystemChatMessage)),
//            System = messages.OfType<SystemChatMessage>().FirstOrDefault() switch
//            {
//                null => null,
//                var x => new SystemModel(x.Content[0].Text)
//            },
//        };
//    }

//    static List<MessageParam> ToAnthropicMessages(IEnumerable<ChatMessage> messages)
//    {
//        List<ChatMessage> mergedToolMessages = MergeToolMessages(messages);

//        return [.. mergedToolMessages.Select(ToAnthropicMessage)];

//        static List<ChatMessage> MergeToolMessages(IEnumerable<ChatMessage> messages)
//        {
//            // openai will omit tool messages, but anthropic needs them merged into the user message
//            // for example:
//            // openai: [user, assistant(request tool call, probably multiple), tool(tool response 1), tool(tool response 2), assistant]
//            // anthropic: [user, assistant(request tool call, probably multiple), user(tool response 1 + 2), assistant]
//            List<ChatMessage> result = [];
//            List<ToolChatMessage> toolBuffer = [];

//            foreach (ChatMessage message in messages)
//            {
//                if (message is ToolChatMessage toolMessage)
//                {
//                    toolBuffer.Add(toolMessage);
//                }
//                else
//                {
//                    if (toolBuffer.Count > 0)
//                    {
//                        result.Add(new UserChatMessage(toolBuffer.SelectMany(x => x.Content)));
//                        toolBuffer.Clear();
//                    }
//                    result.Add(message);
//                }
//            }

//            if (toolBuffer.Count > 0)
//            {
//                result.Add(new UserChatMessage(toolBuffer.SelectMany(x => x.Content)));
//            }

//            return result;
//        }

//        static MessageParam ToAnthropicMessage(ChatMessage message)
//        {
//            Role anthropicRole = message switch
//            {
//                UserChatMessage => Role.User,
//                AssistantChatMessage => Role.Assistant,
//                ToolChatMessage => throw new InvalidOperationException("Tool messages should be merged into user messages before conversion."),
//                SystemChatMessage => throw new InvalidOperationException("System messages should be handled separately."),
//                _ => throw new InvalidOperationException($"Unknown message type: {message.GetType().FullName}"),
//            };

//            List<ContentBlockParam> contents = new(capacity: message.Content.Count + 1);
//            contents.AddRange([.. message.Content.Select(ToAnthropicMessageContent)]);


//            return new MessageParam()
//            {
//                Role = anthropicRole,
//                Content = new MessageParamContent([.. message.Content.Select(ToAnthropicMessageContent)])
//            };

//            static ContentBlockParam ToAnthropicMessageContent(ChatMessageContentPart part)
//            {
//                return part switch
//                {

//                    { Kind: ChatMessageContentPartKind.Text, Text: not null } textPart => new TextBlockParam(part.Text),
//                    { Kind: ChatMessageContentPartKind.Text, Text: null } textPart => ProcessThinking(part),
//                    { Kind: ChatMessageContentPartKind.Image, ImageUri: not null } => new ImageBlockParam(new URLImageSource(part.ImageUri.ToString())),
//                    { Kind: ChatMessageContentPartKind.Image, ImageBytes.Length: > 0 } => new ImageBlockParam(new Base64ImageSource()
//                    {
//                        Data = Convert.ToBase64String(part.ImageBytes.ToArray()),
//                        MediaType = part.ImageBytesMediaType,
//                    }),
//                    _ => throw new InvalidOperationException($"Unknown message content part type: {part.GetType().FullName}"),
//                };

//                static ContentBlockParam ProcessThinking(ChatMessageContentPart part)
//                {
//                    if (part.Patch.TryGetJson("thinking_signature"u8, out ReadOnlyMemory<byte> thinkingSignature))
//                    {
//                        if (thinkingSignature.Length > 0)
//                        {
//                            string signature = JsonSerializer.Deserialize<string>(thinkingSignature.Span)!;
//                            if (part.Patch.TryGetJson("thinking_content"u8, out ReadOnlyMemory<byte> thinkingContent))
//                            {
//                                string thinking = JsonSerializer.Deserialize<string>(thinkingContent.Span)!;
//                                return new ThinkingBlockParam()
//                                {
//                                    Signature = signature,
//                                    Thinking = thinking
//                                };
//                            }
//                            else
//                            {
//                                // only signature present, probably redacted thinking
//                                return new RedactedThinkingBlockParam(signature);
//                            }
//                        }
//                    }
//                    throw new Exception("Thinking content part is missing required thinking_signature field for Anthropic message api.");
//                }
//            }
//        }
//    }
//}
