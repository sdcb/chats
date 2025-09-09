namespace Chats.BE.Controllers.Admin.ModelKeys.Dtos;

public record ReorderRequest<T> where T : struct
{
    public required T SourceId { get; init; }
    public required T? BeforeId { get; init; }
    public required T? AfterId { get; init; }
}
