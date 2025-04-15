namespace Chats.BE.Controllers.Users.Usages.Dtos;

public interface IUsageQuery
{
    public string? User { get; }

    public string? ApiKeyId { get; }

    public string? Provider { get;   }

    public DateTime? Start { get; }

    public DateTime? End { get; }

    public short TimezoneOffset { get; }
}
