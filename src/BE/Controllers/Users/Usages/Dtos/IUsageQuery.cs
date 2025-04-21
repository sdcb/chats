namespace Chats.BE.Controllers.Users.Usages.Dtos;

public interface IUsageQuery
{
    public string? User { get; }

    public string? ApiKeyId { get; }

    public string? Provider { get;   }

    public DateOnly? Start { get; }

    public DateOnly? End { get; }

    public UsageQueryType? Source { get; }

    public short TimezoneOffset { get; }
}
