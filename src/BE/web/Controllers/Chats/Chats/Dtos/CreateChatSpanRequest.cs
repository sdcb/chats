using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record CreateChatSpanRequest
{
    [JsonPropertyName("modelId")]
    public short ModelId { get; init; }

    internal static int MaxSpanCount = 10;

    /// <summary>
    /// Finds the next available SpanId for a new ChatSpan.
    /// </summary>
    /// <param name="spans">The SpanId desc ordered collection of existing ChatSpans.</param>
    /// <returns>The next available SpanId.</returns>
    internal static byte FindAvailableSpanId(ICollection<byte> spanIds)
    {
        if (spanIds.Count == 0)
        {
            return 0;
        }

        // Suggest the next SpanId based on the max SpanId in the collection
        byte suggested = spanIds.Max();
        if (suggested < 255)
        {
            // If the suggested SpanId is less than 255, increment it by 1
            return (byte)(suggested + 1);
        }
        else
        {
            // If the suggested SpanId is 255, find the first available SpanId starting from 0
            byte spanId = 0;
            while (spanIds.Any(x => x == spanId))
            {
                spanId++;
            }
            return spanId;
        }
    }
}
