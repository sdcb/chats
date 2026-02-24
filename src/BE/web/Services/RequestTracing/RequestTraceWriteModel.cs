namespace Chats.BE.Services.RequestTracing;

public abstract class RequestTraceWriteModel
{
    public DateTime StartedAt { get; init; }
    public RequestTraceDirection Direction { get; init; }
    public string? Source { get; init; }
    public int? UserId { get; init; }
    public string? TraceId { get; init; }
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = "/";
}

public sealed class RequestTraceRequestHeaderWriteModel : RequestTraceWriteModel
{
    public string? RequestContentType { get; init; }
    public string RequestHeaders { get; init; } = string.Empty;
}

public sealed class RequestTraceRequestBodyWriteModel : RequestTraceWriteModel
{
    public string? RequestContentType { get; init; }
    public int RawRequestBodyBytes { get; init; }
    public bool IsRequestBodyTruncated { get; init; }
    public string? RequestBody { get; init; }
    public byte[]? RequestBodyRaw { get; init; }
}

public sealed class RequestTraceResponseHeaderWriteModel : RequestTraceWriteModel
{
    public int DurationMs { get; init; }
    public string? ResponseContentType { get; init; }
    public short? StatusCode { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResponseHeaders { get; init; }
}

public sealed class RequestTraceResponseBodyWriteModel : RequestTraceWriteModel
{
    public int DurationMs { get; init; }
    public string? ResponseContentType { get; init; }
    public short? StatusCode { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public int? RawResponseBodyBytes { get; init; }
    public bool IsResponseBodyTruncated { get; init; }
    public string? ResponseBody { get; init; }
    public byte[]? ResponseBodyRaw { get; init; }
}
