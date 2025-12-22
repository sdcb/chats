using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Chats.Web.Services.Models;

public record ModelValidateResult
{
    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; init; }

    [JsonPropertyName("errorMessage")]
    public required string? ErrorMessage { get; init; }

    [SetsRequiredMembers]
    private ModelValidateResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static ModelValidateResult Success() => new(true, null);
    public static ModelValidateResult Fail(string errorMessage) => new(false, errorMessage);
}
