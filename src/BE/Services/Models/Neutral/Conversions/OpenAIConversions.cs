using OpenAI.Chat;

namespace Chats.BE.Services.Models.Neutral.Conversions;

/// <summary>
/// Conversion methods between OpenAI ChatMessage and NeutralMessage.
/// </summary>
public static class OpenAIConversions
{
    /// <summary>
    /// Converts an OpenAI ChatMessage to a NeutralMessage.
    /// </summary>
    public static NeutralMessage ToNeutral(this ChatMessage message)
    {
        NeutralChatRole role = message switch
        {
            UserChatMessage => NeutralChatRole.User,
            AssistantChatMessage => NeutralChatRole.Assistant,
            ToolChatMessage => NeutralChatRole.Tool,
            _ => throw new NotSupportedException($"Chat message type {message.GetType().Name} is not supported.")
        };

        List<NeutralContent> contents = [];

        if (message is ToolChatMessage tool)
        {
            contents.Add(NeutralToolCallResponseContent.Create(
                tool.ToolCallId,
                tool.Content[0].Text));
        }
        else
        {
            foreach (ChatMessageContentPart part in message.Content)
            {
                contents.AddRange(part.ToNeutral());
            }
        }

        return new NeutralMessage
        {
            Role = role,
            Contents = contents
        };
    }

    /// <summary>
    /// Converts an OpenAI ChatMessageContentPart to NeutralContent(s).
    /// </summary>
    public static IEnumerable<NeutralContent> ToNeutral(this ChatMessageContentPart part)
    {
        switch (part.Kind)
        {
            case ChatMessageContentPartKind.Image when part.ImageUri != null:
                yield return NeutralFileUrlContent.Create(part.ImageUri.ToString());
                break;

            case ChatMessageContentPartKind.Image when !part.ImageBytes.IsEmpty:
                yield return NeutralFileBlobContent.Create(part.ImageBytes.ToArray(), part.ImageBytesMediaType);
                break;

            case ChatMessageContentPartKind.Text:
                // Check for reasoning_content in patched data
                if (part.Patch.TryGetValue("reasoning_content"u8, out string? reasoningContent) && !string.IsNullOrEmpty(reasoningContent))
                {
                    byte[]? signatureBytes = null;
                    if (part.Patch.TryGetValue("signature"u8, out string? signature) && !string.IsNullOrEmpty(signature))
                    {
                        signatureBytes = Convert.FromBase64String(signature);
                    }
                    yield return NeutralThinkContent.Create(reasoningContent, signatureBytes);
                }

                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return NeutralTextContent.Create(part.Text);
                }
                break;

            default:
                throw new NotSupportedException($"ChatMessageContentPart kind {part.Kind} is not supported.");
        }
    }

    /// <summary>
    /// Converts a collection of OpenAI ChatMessages to a list of NeutralMessages.
    /// Excludes system messages (they should be handled separately).
    /// </summary>
    public static IList<NeutralMessage> ToNeutralExcludingSystem(this IEnumerable<ChatMessage> messages)
    {
        return messages
            .Where(m => m is not SystemChatMessage and not DeveloperChatMessage)
            .Select(m => m.ToNeutral())
            .ToList();
    }

    /// <summary>
    /// Extracts system prompt from OpenAI ChatMessages.
    /// </summary>
    public static string? ExtractSystemPrompt(this IEnumerable<ChatMessage> messages)
    {
        string combined = string.Join("\r\n", messages
            .Where(x => x is SystemChatMessage or DeveloperChatMessage)
            .Select(x => string.Join("\r\n", x.Content.Select(c => c.Text))));

        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }
}
