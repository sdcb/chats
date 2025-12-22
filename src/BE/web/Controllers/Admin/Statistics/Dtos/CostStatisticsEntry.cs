namespace Chats.Web.Controllers.Admin.Statistics.Dtos;

public record CostStatisticsEntry
{
    public decimal InputCost { get; init; }
    public decimal OutputCost { get; init; }
}
