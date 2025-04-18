namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record CostStatisticsEntry
{
    public decimal InputCost { get; init; }
    public decimal OutputCost { get; init; }
}
