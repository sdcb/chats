using System.Text.Json.Serialization;

namespace Chats.BE.Services.Configs;

/// <summary>
/// 单一方向的请求追踪配置（可用于入站或出站）。
/// </summary>
public record RequestTraceConfig
{
    /// <summary>
    /// 是否启用该方向追踪。
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 采样率，取值范围建议为 0~1。
    /// </summary>
    [JsonPropertyName("sampleRate")]
    public double SampleRate { get; init; } = 1;

    /// <summary>
    /// 过滤规则配置。
    /// </summary>
    [JsonPropertyName("filters")]
    public RequestTraceFilters Filters { get; init; } = new();

    /// <summary>
    /// 请求头/响应头采集配置。
    /// </summary>
    [JsonPropertyName("headers")]
    public RequestTraceHeaderConfig Headers { get; init; } = new();

    /// <summary>
    /// 请求体/响应体采集配置。
    /// </summary>
    [JsonPropertyName("body")]
    public RequestTraceBodyConfig Body { get; init; } = new();
}

/// <summary>
/// 请求追踪过滤条件。
/// </summary>
public record RequestTraceFilters
{
    /// <summary>
    /// 限定命中的来源名称模式列表；入站表示IP地址，出站可表示 HttpClient 名称。
    /// 为 null 表示不按名称限制（允许所有）。
    /// </summary>
    [JsonPropertyName("sourcePatterns")]
    public string[]? SourcePatterns { get; init; } = null;

    /// <summary>
    /// 仅包含这些 URL 模式（可使用通配符）；为 null 表示不过滤（允许所有）。
    /// </summary>
    [JsonPropertyName("includeUrlPatterns")]
    public string[]? IncludeUrlPatterns { get; init; } = null;

    /// <summary>
    /// 排除这些 URL 模式（可使用通配符）；为 null 表示不过滤（允许所有）。
    /// </summary>
    [JsonPropertyName("excludeUrlPatterns")]
    public string[]? ExcludeUrlPatterns { get; init; } = null;

    /// <summary>
    /// 仅包含这些 HTTP 方法（如 GET、POST）；为 null 表示不过滤（允许所有）。
    /// </summary>
    [JsonPropertyName("methods")]
    public string[]? Methods { get; init; } = null;

    /// <summary>
    /// 仅包含这些状态码规则；为 null 表示不过滤（允许所有）。
    /// 支持精确状态码（如 200、429）和分组写法（如 2xx、4xx、5xx）。
    /// </summary>
    [JsonPropertyName("statusCodes")]
    public string[]? StatusCodes { get; init; } = null;

    /// <summary>
    /// 最小耗时阈值（毫秒），仅记录耗时不低于该值的请求。
    /// </summary>
    [JsonPropertyName("minDurationMs")]
    public int? MinDurationMs { get; init; }
}

/// <summary>
/// 请求头与响应头采集配置。
/// </summary>
public record RequestTraceHeaderConfig
{
    /// <summary>
    /// 仅包含这些请求头（不区分大小写）；为 null 表示不过滤（允许所有）。
    /// </summary>
    [JsonPropertyName("includeRequestHeaders")]
    public string[]? IncludeRequestHeaders { get; init; } = null;

    /// <summary>
    /// 仅包含这些响应头（不区分大小写）；为 null 表示不过滤（允许所有）。
    /// </summary>
    [JsonPropertyName("includeResponseHeaders")]
    public string[]? IncludeResponseHeaders { get; init; } = null;

    /// <summary>
    /// 需要脱敏的请求头名称列表（不区分大小写），默认包含常见敏感头。
    /// </summary>
    [JsonPropertyName("redactRequestHeaders")]
    public string[] RedactRequestHeaders { get; init; } = ["authorization", "cookie", "x-api-key", "proxy-authorization"];

    /// <summary>
    /// 需要脱敏的响应头名称列表（不区分大小写），默认包含常见敏感头。
    /// </summary>
    [JsonPropertyName("redactResponseHeaders")]
    public string[] RedactResponseHeaders { get; init; } = ["set-cookie"];
}

/// <summary>
/// 请求体与响应体采集配置。
/// </summary>
public record RequestTraceBodyConfig
{
    /// <summary>
    /// 是否记录请求体（文本形式）。
    /// </summary>
    [JsonPropertyName("captureRequestBody")]
    public bool CaptureRequestBody { get; init; } = true;

    /// <summary>
    /// 是否记录响应体（文本形式）。
    /// </summary>
    [JsonPropertyName("captureResponseBody")]
    public bool CaptureResponseBody { get; init; } = true;

    /// <summary>
    /// 是否记录原始请求体（二进制，未解压、未 de-chunk、尽量按原样）。
    /// </summary>
    [JsonPropertyName("captureRawRequestBody")]
    public bool CaptureRawRequestBody { get; init; } = false;

    /// <summary>
    /// 是否记录原始响应体（二进制，未解压、未 de-chunk、尽量按原样）。
    /// </summary>
    [JsonPropertyName("captureRawResponseBody")]
    public bool CaptureRawResponseBody { get; init; } = false;

    /// <summary>
    /// 单次请求体或响应体可记录的最大字节数，超出将截断；默认 5MB。
    /// </summary>
    [JsonPropertyName("maxBytes")]
    public int MaxBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// 允许采集 Body 的 Content-Type 前缀列表；为 null 表示允许所有类型。
    /// </summary>
    [JsonPropertyName("allowedContentTypes")]
    public string[]? AllowedContentTypes { get; init; } = null;

    /// <summary>
    /// 在 JSON Body 中需要脱敏的字段名列表，默认包含常见敏感字段。
    /// </summary>
    [JsonPropertyName("redactJsonFields")]
    public string[] RedactJsonFields { get; init; } = ["password", "token", "secret", "apiKey", "access_token", "refresh_token"];
}
