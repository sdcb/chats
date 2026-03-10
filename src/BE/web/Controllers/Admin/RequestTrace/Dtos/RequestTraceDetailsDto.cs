namespace Chats.BE.Controllers.Admin.RequestTrace.Dtos;

public record RequestTraceDetailsDto : RequestTraceListItemDto
{
    public string? ErrorMessage { get; init; }

    public string? RequestHeaders { get; init; }

    public string? ResponseHeaders { get; init; }

    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }
}
