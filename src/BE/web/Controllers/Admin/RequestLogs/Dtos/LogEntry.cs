using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.RequestLogs.Dtos;

public record LogEntry
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("ip")]
    public required string? Ip { get; init; }

    [JsonPropertyName("userId")]
    public required Guid? UserId { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("statusCode")]
    public required int StatusCode { get; init; }

    [JsonPropertyName("responseTime")]
    public required string ResponseTime { get; init; }

    // TODO: RequestTime should be number/DateTime instead of String(need FE effort)
    [JsonPropertyName("requestTime")]
    public required string RequestTime { get; init; }

    // TODO: RequestTime should be number/DateTime instead of String(need FE effort)
    [JsonPropertyName("headers")]
    public required string Headers { get; init; }

    [JsonPropertyName("request")]
    public required string Request { get; init; }

    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("user")]
    public required OnlyUserName? User { get; init; }
}