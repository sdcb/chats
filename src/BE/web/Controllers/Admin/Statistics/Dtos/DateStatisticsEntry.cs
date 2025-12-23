using System.Diagnostics.CodeAnalysis;

namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record DateStatisticsEntry<T>
{
    public required DateOnly Date { get; init; }
    public required T Value { get; init; }

    [SetsRequiredMembers]
    public DateStatisticsEntry(DateOnly date, T value)
    {
        Date = date;
        Value = value;
    }
}
