namespace Chats.BE.Controllers.Users.Usages.Dtos;

public record UsageDto
{
    public required string? UserName { get; init; }

    public required string? ApiKey { get; init; }

    public required string ModelProviderName { get; init; }

    public required string ModelReferenceName { get; init; }

    public required string ModelName { get; init; }

    public required int PreprocessDurationMs { get; init; }

    public required int FirstResponseDurationMs { get; init; }

    public required int PostprocessDurationMs { get; init; }

    public required int TotalDurationMs { get; init; }

    public required string FinishReason { get; init; }

    public required string UserAgent { get; init; }

    public required string IP { get; init; }

    public required int InputTokens { get; init; }

    public required int OutputTokens { get; init; }

    public required int ReasoningTokens { get; init; }

    public required decimal InputCost { get; init; }

    public required decimal OutputCost { get; init; }

    public required DateTime UsagedCreatedAt { get; init; }
}
