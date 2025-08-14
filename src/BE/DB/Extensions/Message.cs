namespace Chats.BE.DB;

public partial class Message
{
    public static Message MergeAll(IReadOnlyCollection<Message> messages)
    {
        if (messages.Count == 0) throw new ArgumentException("Cannot merge an empty collection of messages.", nameof(messages));
        Message first = messages.First();
        Message last = messages.Last();
        if (messages.Count == 1) return first;

        Message merged = new()
        {
            Id = last.Id,
            ChatId = first.ChatId,
            ChatRoleId = first.ChatRoleId,
            CreatedAt = first.CreatedAt,
            Edited = first.Edited,
            ParentId = first.ParentId,
            SpanId = first.SpanId,
            MessageResponse = new MessageResponse()
            {
                Usage = first.MessageResponse!.Usage,
                ChatConfig = first.MessageResponse!.ChatConfig,
                ReactionId = first.MessageResponse!.ReactionId,
            }
        };
        foreach (Message message in messages)
        {
            foreach (var content in message.MessageContents)
            {
                merged.MessageContents.Add(content);
            }
            if (message != first)
            {
                merged.MessageResponse.Usage.InputCost += message.MessageResponse?.Usage.InputCost ?? 0;
                merged.MessageResponse.Usage.OutputCost += message.MessageResponse?.Usage.OutputCost ?? 0;
                merged.MessageResponse.Usage.InputTokens += message.MessageResponse?.Usage.InputTokens ?? 0;
                merged.MessageResponse.Usage.OutputTokens += message.MessageResponse?.Usage.OutputTokens ?? 0;
                merged.MessageResponse.Usage.ReasoningTokens += message.MessageResponse?.Usage.ReasoningTokens ?? 0;
                merged.MessageResponse.Usage.SegmentCount += message.MessageResponse?.Usage.SegmentCount ?? 0;
            }
        }

        return merged;
    }
}
