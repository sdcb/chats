﻿using Chats.BE.Infrastructure;
using Chats.BE.Services.Conversations.Dtos;
using Sdcb.WenXinQianFan;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;
using ChatMessageContentPartKind = OpenAI.Chat.ChatMessageContentPartKind;

namespace Chats.BE.Services.Conversations.Implementations.QianFan;

public class QianFanConversationService : ConversationService
{
    private QianFanClient ChatClient { get; }

    private JsonQianFanModelConfig GlobalModelConfig { get; }

    public QianFanConversationService(string keyConfigText, string modelConfigText)
    {
        JsonQianFanApiConfig apiConfig = JsonSerializer.Deserialize<JsonQianFanApiConfig>(keyConfigText)!;
        ChatClient = new QianFanClient(apiConfig.ApiKey, apiConfig.Secret);
        GlobalModelConfig = JsonSerializer.Deserialize<JsonQianFanModelConfig>(modelConfigText)!;
    }

    public override async IAsyncEnumerable<ConversationSegment> ChatStreamed(IReadOnlyList<OpenAIChatMessage> messages, ModelConfig config, CurrentUser currentUser, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        KnownModel model = new(GlobalModelConfig.Model);
        ChatMessage[] qianFanMessages = messages
            .Where(x => x is UserChatMessage || x is AssistantChatMessage)
            .Select(OpenAIMessageToQianFan)
            .ToArray();
        ChatRequestParameters chatRequestParameters = new()
        {
            Temperature = config.Temperature ?? GlobalModelConfig.Temperature,
            MaxOutputTokens = config.MaxLength,
            UserId = currentUser.Id.ToString(),
            DisableSearch = !config.EnableSearch,
            System = messages.OfType<SystemChatMessage>().Single().Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text
        };

        await foreach (ChatResponse chatResponse in ChatClient.ChatAsStreamAsync(model, qianFanMessages, chatRequestParameters, cancellationToken))
        {
            yield return new ConversationSegment
            {
                TextSegment = chatResponse.Result,
                InputTokenCount = chatResponse.Usage.PromptTokens,
                OutputTokenCount = chatResponse.Usage.CompletionTokens
            };
        }
    }

    static ChatMessage OpenAIMessageToQianFan(OpenAIChatMessage message)
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
            SystemChatMessage systemMessage => throw new NotSupportedException("System message is not supported in QianFan"),
            AssistantChatMessage assistantMessage => ChatMessage.FromAssistant(assistantMessage.Content.Single(x => x.Kind == ChatMessageContentPartKind.Text).Text),
            _ => throw new ArgumentException($"Unknown message type: {message.GetType()}")
        };
    }

    protected override void Disposing()
    {
        ChatClient.Dispose();
    }
}