using System.Diagnostics.CodeAnalysis;

namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record SingleValueStatisticsEntry
{
    public required string Key { get; init; }

    public required int Count { get; init; }

    [SetsRequiredMembers]
    public SingleValueStatisticsEntry(string key, int count)
    {
        Key = key;
        Count = count;
    }
}
