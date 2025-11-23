using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.Models.ChatServices;

/// <summary>
/// OpenAI “tool_calls” 数组里，单块（片段）对应的模型。
/// </summary>
public sealed record ToolCallSegment : ChatSegmentItem
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    // 原 function.name
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    // 原 function.arguments（此处往往是 JSON 片段）
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }
}

public sealed record ToolCallResponseSegment : ChatSegmentItem
{
    public required string ToolCallId { get; init; }

    public string? Response { get; init; }

    public required int DurationMs { get; init; }

    public required bool IsSuccess { get; init; }

    public StepContentToolCallResponse ToDB()
    {
        return new StepContentToolCallResponse
        {
            ToolCallId = ToolCallId!,
            Response = Response!,
            DurationMs = DurationMs,
            IsSuccess = IsSuccess
        };
    }
}

/// <summary>
/// 组合完毕后的完整函数调用。
/// </summary>
public sealed record ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; } // 完整 JSON 字符串

    /// <summary>
    /// 把连续的 FunctionCallSegment 按 Index 聚合为 FunctionCall。
    /// 默认认为同一 Index 的各片段在流中是连续出现的。
    /// </summary>
    public static async IAsyncEnumerable<ToolCall> From(
        IAsyncEnumerable<ToolCallSegment> segments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int? currentIndex = null;
        string? id = null;
        string? name = null;
        StringBuilder argsBuilder = new();

        await foreach (ToolCallSegment s in segments.WithCancellation(cancellationToken))
        {
            // 遇到新 Index —— 先把上一个完整调用产出
            if (currentIndex.HasValue && s.Index != currentIndex.Value)
            {
                yield return BuildAndReset();
            }

            // 初始化当前分组
            if (!currentIndex.HasValue)
                currentIndex = s.Index;

            // 逐字段补全
            if (s.Id is not null) id = s.Id;
            if (s.Name is not null) name = s.Name;
            if (s.Arguments is not null) argsBuilder.Append(s.Arguments);
        }

        // 流结束后还有残留分组
        if (currentIndex.HasValue)
            yield return BuildAndReset();

        // 本地函数：把已累积的信息构造成 FunctionCall
        ToolCall BuildAndReset()
        {
            try
            {
                // 校验必填字段已补齐；若缺失直接抛异常更易排查
                if (id is null || name is null)
                    throw new InvalidOperationException(
                        $"Incomplete function call for index {currentIndex}");

                return new ToolCall
                {
                    Id = id,
                    Name = name,
                    Arguments = argsBuilder.ToString(),
                };
            }
            finally
            {
                // 清空状态，准备下一组
                currentIndex = null;
                id = name = null;
                argsBuilder.Clear();
            }
        }
    }

    /// <summary>
    /// 把连续的 FunctionCallSegment 按 Index 聚合为 FunctionCall。
    /// 默认认为同一 Index 的各片段在流中是连续出现的。
    /// </summary>
    public static IEnumerable<ToolCall> From(IEnumerable<ToolCallSegment> segments)
    {
        return From(segments.ToAsyncEnumerable())
            .ToBlockingEnumerable();
    }

    public FullToolCall ToOpenAI()
    {
        return new FullToolCall
        {
            Id = Id,
            Function = new FullToolCallFunction()
            {
                Name = Name,
                Arguments = Arguments
            }
        };
    } 
}
