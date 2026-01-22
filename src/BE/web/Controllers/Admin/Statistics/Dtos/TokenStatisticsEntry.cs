namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record TokenStatisticsEntry
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }
    
    public int ReasoningTokens { get; init; }

    public int TotalTokens => InputTokens + OutputTokens;
}
