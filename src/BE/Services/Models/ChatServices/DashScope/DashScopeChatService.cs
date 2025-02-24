using Chats.BE.Services.Models.Dtos;
using Sdcb.DashScope;
using Sdcb.DashScope.TextGeneration;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;
using ChatMessageContentPartKind = OpenAI.Chat.ChatMessageContentPartKind;
using System.Runtime.CompilerServices;
using OpenAI.Chat;
using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using ChatTokenUsage = Sdcb.DashScope.TextGeneration.ChatTokenUsage;
using ChatMessage = Sdcb.DashScope.TextGeneration.ChatMessage;

namespace Chats.BE.Services.Models.ChatServices.DashScope;

public class DashScopeChatService : ChatService
{
    private DashScopeClient Client { get; }
    private TextGenerationClient ChatClient { get; }

    public DashScopeChatService(Model model) : base(model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        Client = new DashScopeClient(model.ModelKey.Secret);
        ChatClient = Client.TextGeneration;
    }

    protected override Task<OpenAIChatMessage[]> FEPreprocess(IReadOnlyList<OpenAIChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (feOptions.WebSearchEnabled && Model.ModelReference.AllowSearch)
        {
            options.SetWebSearchEnabled_QwenStyle(true);
        }
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<OpenAIChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatParameters chatParameters = new()
        {
            Temperature = options.Temperature,
            MaxTokens = options.MaxOutputTokenCount,
            EnableSearch = options.IsSearchEnabled(),
            Seed = options.GetDashScopeSeed(),
            IncrementalOutput = true,
        };

        if (Model.ModelReference.AllowVision)
        {
            ChatVLMessage[] msgs = messages.Select(OpenAIMessageToQwenVL).ToArray();
            await foreach (ResponseWrapper<string, ChatTokenUsage> resp in ChatClient.ChatVLStreamed(Model.ApiModelId, msgs, chatParameters, cancellationToken))
            {
                yield return new ChatSegment
                {
                    ReasoningSegment = null,
                    Segment = resp.Output,
                    FinishReason = null,
                    Usage = resp.Usage != null ? new Dtos.ChatTokenUsage
                    {
                        InputTokens = resp.Usage.InputTokens,
                        OutputTokens = resp.Usage.OutputTokens,
                        ReasoningTokens = 0,
                    } : null,
                };
            }
        }
        else
        {
            ChatMessage[] msgs = messages.Select(OpenAIMessageToQwen).ToArray();
            await foreach (ResponseWrapper<ChatResponse, ChatTokenUsage> resp in ChatClient.ChatStreamed(Model.ApiModelId, msgs, chatParameters, cancellationToken))
            {
                yield return new ChatSegment
                {
                    ReasoningSegment = null,
                    Segment = resp.Output.Choices[0].Message.Content,
                    FinishReason = ToFinishReason(resp.Output.Choices[0].FinishReason),
                    Usage = resp.Usage != null ? new Dtos.ChatTokenUsage
                    {
                        InputTokens = resp.Usage.InputTokens,
                        OutputTokens = resp.Usage.OutputTokens,
                        ReasoningTokens = 0,
                    } : null,
                };
            }
        }
    }

    static ChatFinishReason? ToFinishReason(string reason)
    {
        return reason switch
        {
            "" => null,
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "tool_calls" => ChatFinishReason.ToolCalls,
            _ => null,
        };
    }

    static ChatMessage OpenAIMessageToQwen(OpenAIChatMessage message)
    {
        return message switch
        {
            UserChatMessage userMessage => ChatMessage.FromUser(string.Join("\r\n", userMessage.Content
                .Select(x => x.Kind switch
                {
                    var v when v == ChatMessageContentPartKind.Text => x.Text,
                    var v when v == ChatMessageContentPartKind.Image => x.ImageUri.ToString(),
                    _ => throw new ArgumentException("Unknown content part kind")
                }))), 
            SystemChatMessage systemMessage => ChatMessage.FromSystem(systemMessage.Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text),
            AssistantChatMessage assistantMessage => ChatMessage.FromAssistant(assistantMessage.Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text),
            _ => throw new ArgumentException($"Unknown message type: {message.GetType()}")
        };
    }

    static ChatVLMessage OpenAIMessageToQwenVL(OpenAIChatMessage message)
    {
        return message switch
        {
            UserChatMessage userMessage => ChatVLMessage.FromUser(userMessage.Content
                .Select(x => x.Kind switch
                {
                    var v when v == ChatMessageContentPartKind.Text => (ContentItem)ContentItem.FromText(x.Text),
                    var v when v == ChatMessageContentPartKind.Image => ContentItem.FromImage(x.ImageUri.ToString()),
                    _ => throw new ArgumentException("Unknown content part kind")
                })
                .ToArray()),
            SystemChatMessage systemMessage => ChatVLMessage.FromSystem(systemMessage.Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text),
            AssistantChatMessage assistantMessage => ChatVLMessage.FromAssistant(assistantMessage.Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text),
            _ => throw new ArgumentException($"Unknown message type: {message.GetType()}")
        };
    }

    protected override void Disposing()
    {
        Client.Dispose();
    }
}
