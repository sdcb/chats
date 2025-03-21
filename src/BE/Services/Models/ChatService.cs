using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;
using OpenAI.Chat;
using System.Text;
using Microsoft.ML.Tokenizers;
using Chats.BE.Services.Models.Extensions;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService : IDisposable
{
    public const float DefaultTemperature = 0.4f;
    public const string DefaultPrompt = "你是{{MODEL_NAME}}，请仔细遵循用户指令并认真回复，当前日期: {{CURRENT_DATE}}";

    internal protected Model Model { get; }
    internal protected Tokenizer Tokenizer { get; }

    internal static Tokenizer DefaultTokenizer { get; } = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public ChatService(Model model)
    {
        Model = model;
        if (model.ModelReference.Tokenizer is not null)
        {
            Tokenizer = TiktokenTokenizer.CreateForEncoding(model.ModelReference.Tokenizer.Name);
        }
        else
        {
            Tokenizer = DefaultTokenizer;
        }
    }

    public abstract IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken);

    public virtual async Task<ChatSegment> Chat(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
    {
        StringBuilder content = new();
        StringBuilder reasoningContent = new();
        ChatSegment? lastSegment = null;
        await foreach (ChatSegment seg in ChatStreamed(messages, options, cancellationToken))
        {
            lastSegment = seg;
            content.Append(seg.Segment);
            content.Append(seg.ReasoningSegment);
        }

        return new ChatSegment()
        {
            Usage = lastSegment?.Usage,
            FinishReason = lastSegment?.FinishReason,
            Segment = content.ToString(),
            ReasoningSegment = reasoningContent.ToString(),
        };
    }

    internal protected int GetPromptTokenCount(IReadOnlyList<ChatMessage> messages)
    {
        const int TokenPerConversation = 3;
        int messageTokens = messages.Sum(m => m.CountTokens(Tokenizer));
        return TokenPerConversation + messageTokens;
    }

    protected virtual async Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.AllowSearch)
        {
            SetWebSearchEnabled(options, feOptions.WebSearchEnabled);
        }

        if (!Model.ModelReference.AllowSystemPrompt)
        {
            // Remove system prompt
            messages = messages.Where(m => m is not SystemChatMessage).ToArray();
        }
        else
        {
            // system message transform
            SystemChatMessage? existingSystemPrompt = messages.OfType<SystemChatMessage>().FirstOrDefault();
            DateTime now = feOptions.Now;
            if (existingSystemPrompt is not null)
            {
                existingSystemPrompt.Content[0] = existingSystemPrompt.Content[0].Text
                    .Replace("{{CURRENT_DATE}}", now.ToString("yyyy/MM/dd"))
                    .Replace("{{MODEL_NAME}}", Model.ModelReference.DisplayName ?? Model.ModelReference.Name)
                    .Replace("{{CURRENT_TIME}}", now.ToString("HH:mm:ss"));
                ;
            }
        }

        ChatMessage[] filteredMessage = await messages
            .ToAsyncEnumerable()
            .SelectAwait(async m => await FilterVision(Model.ModelReference.AllowVision, m, cancellationToken))
            .ToArrayAsync(cancellationToken);
        options.Temperature = Model.ModelReference.UnnormalizeTemperature(options.Temperature);

        return filteredMessage;
    }

    protected virtual void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        // chat service not enable search by default, prompt a warning
        Console.WriteLine($"{Model.ModelReference.Name} chat service not support web search.");
    }

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    protected virtual void Disposing() { }
}
