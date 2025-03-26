using Chats.BE.Controllers.Common.Dtos;

namespace Chats.BE.Controllers.Users.Usages.Dtos;

public record UsageQuery : PagingRequest
{
    public string? User { get; init; }

    public string? ApiKey { get; init; }

    public string? Provider { get; init; }

    public DateTime? Start { get; init; }

    public DateTime? End { get; init; }

    public required short TimezoneOffset { get; init; }
}
