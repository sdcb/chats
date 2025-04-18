namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record CostStatisticsEntry
{
    public required decimal InputCost { get; init; }
    public required decimal OutputCost { get; init; }
}
