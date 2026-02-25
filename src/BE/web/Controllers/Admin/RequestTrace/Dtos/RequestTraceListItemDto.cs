namespace Chats.BE.Controllers.Admin.RequestTrace.Dtos;

public record RequestTraceListItemDto
{
    public required long Id { get; init; }

    public required DateTime StartedAt { get; init; }

    public DateTime? RequestBodyAt { get; init; }

    public DateTime? ResponseHeaderAt { get; init; }

    public DateTime? ResponseBodyAt { get; init; }

    public required byte Direction { get; init; }

    public string? Source { get; init; }

    public int? UserId { get; init; }

    public string? UserName { get; init; }

    public string? TraceId { get; init; }

    public required string Method { get; init; }

    public required string Url { get; init; }

    public string? RequestContentType { get; init; }

    public string? ResponseContentType { get; init; }

    public short? StatusCode { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorMessage { get; init; }

    public int RawRequestBodyBytes { get; init; }

    public int? RawResponseBodyBytes { get; init; }

    public bool IsRequestBodyTruncated { get; init; }

    public bool IsResponseBodyTruncated { get; init; }

    public bool HasPayload { get; init; }

    public bool HasRequestBodyRaw { get; init; }

    public bool HasResponseBodyRaw { get; init; }
}
