using Chats.DB.Enums;
using Chats.BE.Controllers.Api.AnthropicCompatible.Dtos;
using Chats.BE.Services.Models.ChatServices;
using System.Text.Json;

namespace Chats.BE.Services.Models.Dtos;

public static class AnthropicSegmentExtensions
{
    /// <summary>
    /// Converts DBFinishReason to Anthropic stop_reason
    /// Anthropic supports: end_turn, max_tokens, stop_sequence, tool_use, pause_turn, refusal
    /// </summary>
    public static string? ToAnthropicStopReason(this DBFinishReason finishReason)
    {
        return finishReason switch
        {
            DBFinishReason.Stop or DBFinishReason.Success => "end_turn",
            DBFinishReason.Length => "max_tokens",
            DBFinishReason.ToolCalls or DBFinishReason.FunctionCall => "tool_use",
            DBFinishReason.ContentFilter => "refusal",
            _ => "end_turn"
        };
    }

    /// <summary>
    /// Converts snapshot to Anthropic response format (non-streaming)
    /// </summary>
    public static AnthropicResponse ToAnthropicResponse(this ChatCompletionSnapshot snapshot, string model, string messageId)
    {
        List<AnthropicResponseContentBlock> content = [];

        foreach (ChatSegment item in snapshot.Segments)
        {
            switch (item)
            {
                case ThinkChatSegment think:
                    content.Add(AnthropicResponseContentBlock.FromThinking(think.Think, think.Signature));
                    break;
                case TextChatSegment text:
                    content.Add(AnthropicResponseContentBlock.FromText(text.Text));
                    break;
                case ToolCallSegment tool when tool.Id != null && tool.Name != null:
                    object input = new { };
                    if (!string.IsNullOrEmpty(tool.Arguments))
                    {
                        try
                        {
                            input = JsonSerializer.Deserialize<object>(tool.Arguments) ?? new { };
                        }
                        catch
                        {
                            input = new { };
                        }
                    }
                    content.Add(AnthropicResponseContentBlock.FromToolUse(tool.Id, tool.Name, input));
                    break;
            }
        }

        return new AnthropicResponse
        {
            Id = messageId,
            Content = content,
            Model = model,
            StopReason = snapshot.FinishReason.ToAnthropicStopReason(),
            Usage = new AnthropicUsage
            {
                InputTokens = snapshot.Usage.InputTokens,
                OutputTokens = snapshot.Usage.OutputTokens,
                CacheCreationInputTokens = snapshot.Usage.CacheCreationTokens,
                CacheReadInputTokens = snapshot.Usage.CacheTokens
            }
        };
    }

    /// <summary>
    /// Creates the message_start event
    /// </summary>
    public static MessageStartEvent ToMessageStartEvent(this ChatCompletionSnapshot snapshot, string model, string messageId)
    {
        return new MessageStartEvent
        {
            Message = new MessageStartData
            {
                Id = messageId,
                Model = model,
                Usage = new MessageStartUsage
                {
                    InputTokens = snapshot.Usage.InputTokens,
                    CacheCreationInputTokens = snapshot.Usage.CacheCreationTokens,
                    CacheReadInputTokens = snapshot.Usage.CacheTokens
                }
            }
        };
    }

    /// <summary>
    /// Creates the message_delta event with final stop reason and usage
    /// </summary>
    public static MessageDeltaEvent ToMessageDeltaEvent(this ChatCompletionSnapshot snapshot)
    {
        return new MessageDeltaEvent
        {
            Delta = new MessageDelta
            {
                StopReason = snapshot.FinishReason.ToAnthropicStopReason()
            },
            Usage = new MessageDeltaUsage
            {
                InputTokens = snapshot.Usage.InputTokens,
                OutputTokens = snapshot.Usage.OutputTokens,
                CacheCreationInputTokens = snapshot.Usage.CacheCreationTokens,
                CacheReadInputTokens = snapshot.Usage.CacheTokens
            }
        };
    }
}
