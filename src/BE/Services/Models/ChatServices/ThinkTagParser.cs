using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.ChatServices;

public record ThinkAndResponseSegment // Think/Response 可以同时存在（但不会同时不存在）
{
    public string? Think { get; init; }
    public string? Response { get; init; }
}

public static class ThinkTagParser
{
    public static async IAsyncEnumerable<ThinkAndResponseSegment> Parse(
        IAsyncEnumerable<string> tokenYielder,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 和原先类似，定义进入/结束 think 模式要匹配的字符串
        const string startThinkTag = "<think>\n";
        // 注意：你的原代码是以 "\n</think>\n\n" 作为结束标记，这里保持一致
        // 假设非严格情况下只要匹配到 \n</think> 就行，但先沿用你给的 endThinkTag。
        const string endThinkTag = "\n</think>\n";

        // 获取枚举器，用于逐个读取 token
        IAsyncEnumerator<string> enumerator = tokenYielder.GetAsyncEnumerator(cancellationToken);

        // 用于在开始时做预读，决定是否开头就是 think 模式
        string? preBuffer = "";

        bool modeDecided = false;
        bool thinkMode = false;   // 是否从开头就进入 Think 模式

        // 预读阶段：判断开头是否要进入 Think 模式（即是否以 "<think>\n" 开头）
        while (!modeDecided)
        {
            if (!await enumerator.MoveNextAsync())
            {
                // 已经没有任何 token 输入，直接结束
                modeDecided = true;
                break;
            }

            string token = enumerator.Current;
            // 若 preBuffer 还是空，并且 token 全是空白，则可以跳过
            if (string.IsNullOrWhiteSpace(preBuffer) && string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            preBuffer += token;

            // 如果预读缓冲区的长度已经足以判断
            if (preBuffer.Length >= startThinkTag.Length)
            {
                // 如果确实是以 "<think>\n" 开头
                if (preBuffer.StartsWith(startThinkTag, StringComparison.Ordinal))
                {
                    // 切去 <think>\n
                    preBuffer = preBuffer[startThinkTag.Length..];
                    thinkMode = true;
                }
                modeDecided = true;
            }
        }

        if (thinkMode)
        {
            // 已确定：开头是 think 模式
            // currentBuffer 用来累积 think 内容或处理拼接
            string currentBuffer = preBuffer;

            // 不同于原先那种“先 yield think，再 yield response”，  
            // 我们在找到结束标记后，会把本次 think + 与结束标记在同一缓冲里紧随的文本 合并到一次返回

            while (true)
            {
                // 在 currentBuffer 中尝试找结束标记
                int index = currentBuffer.IndexOf(endThinkTag, StringComparison.Ordinal);
                if (index >= 0)
                {
                    // 找到结束标记：把它前面的是 think 内容
                    string thinkContent = index > 0
                        ? currentBuffer[..index]
                        : string.Empty;

                    // 跳过结束标记
                    int afterEnd = index + endThinkTag.Length;
                    // 在本次缓冲中，结束标记后剩余的就是 response 内容（如果有的话）
                    string leftover = afterEnd < currentBuffer.Length
                        ? currentBuffer[afterEnd..]
                        : string.Empty;

                    // 合并一次返回
                    yield return new ThinkAndResponseSegment
                    {
                        Think = thinkContent.Length > 0 ? thinkContent : null,
                        Response = leftover.Length > 0 ? leftover : null
                    };

                    // 后面就切换到 response 模式了：退出 think 模式循环
                    break;
                }
                else
                {
                    // 没找到结束标记，要考虑可能的部分匹配
                    int overlap = GetOverlap(currentBuffer, endThinkTag);
                    int emitLength = currentBuffer.Length - overlap;
                    if (emitLength > 0)
                    {
                        // emitLength 这部分肯定是纯 think 内容，且不会匹配结束标记了
                        string sureThinkPart = currentBuffer[..emitLength];
                        yield return new ThinkAndResponseSegment
                        {
                            Think = sureThinkPart,
                            Response = null
                        };
                    }

                    // 更新 currentBuffer，留下与 endThinkTag 有部分重叠的后缀
                    currentBuffer = currentBuffer[emitLength..];

                    // 继续读下一个 token
                    if (!await enumerator.MoveNextAsync())
                    {
                        // 没有更多 token 了，又没找到结束标记，那剩余的都算 think
                        if (currentBuffer.Length > 0)
                        {
                            yield return new ThinkAndResponseSegment
                            {
                                Think = currentBuffer,
                                Response = null
                            };
                        }
                        yield break;
                    }
                    currentBuffer += enumerator.Current;
                }
            }

            // 到这里说明我们结束了 think 模式，后续只需要原样输出 response
            // 读取剩余 token，都当做 response
            while (await enumerator.MoveNextAsync())
            {
                yield return new ThinkAndResponseSegment
                {
                    Think = null,
                    Response = enumerator.Current
                };
            }
        }
        else
        {
            // 若开头不是 think 模式，则 preBuffer 先作为 response 输出（若非空）
            if (!string.IsNullOrEmpty(preBuffer))
            {
                yield return new ThinkAndResponseSegment
                {
                    Think = null,
                    Response = preBuffer
                };
            }

            // 后续所有 token 都作为 response 流式返回
            while (await enumerator.MoveNextAsync())
            {
                yield return new ThinkAndResponseSegment
                {
                    Think = null,
                    Response = enumerator.Current
                };
            }
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