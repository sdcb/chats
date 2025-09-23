using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Common.Dtos;

public record ReorderRequest<T> where T : struct
{
    public required T SourceId { get; init; }
    public required T? PreviousId { get; init; } // 新位置的前一个元素
    public required T? NextId { get; init; }     // 新位置的后一个元素
}
