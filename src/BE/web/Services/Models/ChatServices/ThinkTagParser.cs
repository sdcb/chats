using System.Runtime.CompilerServices;
using Chats.BE.Services.Models.Dtos;

namespace Chats.BE.Services.Models.ChatServices;

public static class ThinkTagParser
{
    public static async IAsyncEnumerable<ChatSegment> Parse(
        IAsyncEnumerable<ChatSegment> segments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string startThinkTag = "<think>";
        const string endThinkTag = "</think>";

        string preBuffer = string.Empty;
        string thinkBuffer = string.Empty;

        bool modeDecided = false;
        bool thinkMode = false;

        await foreach (ChatSegment segment in segments.WithCancellation(cancellationToken))
        {
            if (segment is not TextChatSegment textSegment)
            {
                yield return segment;
                continue;
            }

            string token = textSegment.Text;

            if (!modeDecided)
            {
                preBuffer += token;

                if (preBuffer.Length > startThinkTag.Length)
                {
                    if (preBuffer.StartsWith(startThinkTag, StringComparison.Ordinal))
                    {
                        modeDecided = true;
                        thinkMode = true;
                        token = preBuffer[startThinkTag.Length..];
                        preBuffer = string.Empty;
                    }
                    else
                    {
                        modeDecided = true;
                        thinkMode = false;
                        yield return ChatSegment.FromText(preBuffer);
                        preBuffer = string.Empty;
                        continue;
                    }
                }
                else if (preBuffer.Equals(startThinkTag, StringComparison.Ordinal))
                {
                    modeDecided = true;
                    thinkMode = true;
                    preBuffer = string.Empty;
                    continue;
                }
                else if (!startThinkTag.StartsWith(preBuffer, StringComparison.Ordinal))
                {
                    modeDecided = true;
                    thinkMode = false;
                    yield return ChatSegment.FromText(preBuffer);
                    preBuffer = string.Empty;
                    continue;
                }
                else
                {
                    continue;
                }
            }

            if (!thinkMode)
            {
                if (!string.IsNullOrEmpty(token))
                {
                    yield return ChatSegment.FromText(token);
                }
                continue;
            }

            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            thinkBuffer += token;
            while (thinkBuffer.Length > 0)
            {
                int index = thinkBuffer.IndexOf(endThinkTag, StringComparison.Ordinal);
                if (index >= 0)
                {
                    string thinkPart = index > 0 ? thinkBuffer[..index] : string.Empty;
                    if (!string.IsNullOrEmpty(thinkPart))
                    {
                        yield return ChatSegment.FromThink(thinkPart);
                    }

                    int afterEnd = index + endThinkTag.Length;
                    string responsePart = afterEnd < thinkBuffer.Length ? thinkBuffer[afterEnd..] : string.Empty;
                    if (!string.IsNullOrEmpty(responsePart))
                    {
                        yield return ChatSegment.FromText(responsePart);
                    }

                    thinkBuffer = string.Empty;
                    thinkMode = false;
                    break;
                }

                int overlap = GetOverlap(thinkBuffer, endThinkTag);
                int emitLength = thinkBuffer.Length - overlap;
                if (emitLength <= 0)
                {
                    break;
                }

                string sureThinkPart = thinkBuffer[..emitLength];
                if (!string.IsNullOrEmpty(sureThinkPart))
                {
                    yield return ChatSegment.FromThink(sureThinkPart);
                }

                thinkBuffer = thinkBuffer[emitLength..];
            }
        }

        if (!modeDecided && preBuffer.Length > 0)
        {
            yield return ChatSegment.FromText(preBuffer);
        }

        if (thinkMode && thinkBuffer.Length > 0)
        {
            yield return ChatSegment.FromThink(thinkBuffer);
        }

        // 和原代码一致，用于判断 currentBuffer 的后缀和 endThinkTag 的前缀最大重叠长度
        static int GetOverlap(string s, string pattern)
        {
            int maxOverlap = Math.Min(s.Length, pattern.Length);
            for (int len = maxOverlap; len > 0; len--)
            {
                if (s[^len..].Equals(pattern[..len], StringComparison.Ordinal))
                {
                    return len;
                }
            }
            return 0;
        }
    }
}