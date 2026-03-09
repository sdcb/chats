namespace Chats.BE.Services.RequestTracing;

public abstract class RequestTraceWriteModel
{
    public Guid LogId { get; init; }
}

public abstract class RequestTraceHttpWriteModel : RequestTraceWriteModel
{
    public DateTime StartedAt { get; init; }
    public DateTime? ScheduledDeleteAt { get; init; }
    public RequestTraceDirection Direction { get; init; }
    public string? Source { get; init; }
    public int? UserId { get; init; }
    public string? TraceId { get; init; }
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = "/";
}

public sealed class RequestTraceRequestHeaderWriteModel : RequestTraceHttpWriteModel
{
    public string? RequestContentType { get; init; }
    public string RequestHeaders { get; init; } = string.Empty;
}

public sealed class RequestTraceRequestBodyWriteModel : RequestTraceHttpWriteModel
{
    public DateTime RequestBodyAt { get; init; }
    public string? RequestContentType { get; init; }
    public int RawRequestBodyBytes { get; init; }
    public int RequestBodyLength { get; init; }
    public string? RequestBody { get; init; }
    public byte[]? RequestBodyRaw { get; init; }
}

public sealed class RequestTraceResponseHeaderWriteModel : RequestTraceHttpWriteModel
{
    public DateTime ResponseHeaderAt { get; init; }
    public string? ResponseContentType { get; init; }
    public short? StatusCode { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResponseHeaders { get; init; }
}

public sealed class RequestTraceResponseBodyWriteModel : RequestTraceHttpWriteModel
{
    public DateTime ResponseBodyAt { get; init; }
    public string? ResponseContentType { get; init; }
    public short? StatusCode { get; init; }
    public int? RawResponseBodyBytes { get; init; }
    public int? ResponseBodyLength { get; init; }
    public string? ResponseBody { get; init; }
    public byte[]? ResponseBodyRaw { get; init; }
}

public sealed class RequestTraceExceptionWriteModel : RequestTraceHttpWriteModel
{
    public DateTime ExceptionAt { get; init; }
    public string? ResponseContentType { get; init; }
    public short? StatusCode { get; init; }
    public string ErrorType { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class RequestTraceDeleteWriteModel : RequestTraceWriteModel
{
}
