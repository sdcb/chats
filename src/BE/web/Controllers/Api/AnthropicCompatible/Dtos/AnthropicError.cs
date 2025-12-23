using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Api.AnthropicCompatible.Dtos;

/// <summary>
/// Anthropic API error response format
/// </summary>
public record AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; } = "error";

    [JsonPropertyName("error")]
    public required AnthropicErrorDetail Error { get; init; }
}

public record AnthropicErrorDetail
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public static class AnthropicErrorTypes
{
    public const string InvalidRequestError = "invalid_request_error";
    public const string AuthenticationError = "authentication_error";
    public const string PermissionError = "permission_error";
    public const string NotFoundError = "not_found_error";
    public const string RateLimitError = "rate_limit_error";
    public const string ApiError = "api_error";
    public const string OverloadedError = "overloaded_error";
}
