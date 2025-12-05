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
    /// Converts InternalChatSegment to Anthropic response format (non-streaming)
    /// </summary>
    public static AnthropicResponse ToAnthropicResponse(this InternalChatSegment segment, string model, string messageId, DBFinishReason finishReason)
    {
        List<AnthropicResponseContentBlock> content = [];

        foreach (ChatSegmentItem item in segment.Items)
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
            StopReason = finishReason.ToAnthropicStopReason(),
            Usage = new AnthropicUsage
            {
                InputTokens = segment.Usage.InputTokens,
                OutputTokens = segment.Usage.OutputTokens,
                CacheReadInputTokens = segment.Usage.CacheTokens
            }
        };
    }

    /// <summary>
    /// Creates the message_start event
    /// </summary>
    public static MessageStartEvent ToMessageStartEvent(this InternalChatSegment segment, string model, string messageId)
    {
        return new MessageStartEvent
        {
            Message = new MessageStartData
            {
                Id = messageId,
                Model = model,
                Usage = new MessageStartUsage
                {
                    InputTokens = segment.Usage.InputTokens
                }
            }
        };
    }

    /// <summary>
    /// Creates the message_delta event with final stop reason and usage
    /// </summary>
    public static MessageDeltaEvent ToMessageDeltaEvent(this InternalChatSegment segment, DBFinishReason finishReason)
    {
        return new MessageDeltaEvent
        {
            Delta = new MessageDelta
            {
                StopReason = finishReason.ToAnthropicStopReason()
            },
            Usage = new MessageDeltaUsage
            {
                InputTokens = segment.Usage.InputTokens,
                OutputTokens = segment.Usage.OutputTokens,
                CacheCreationInputTokens = 0,
                CacheReadInputTokens = segment.Usage.CacheTokens
            }
        };
    }
}
