namespace Chats.Web.Controllers.Admin.SecurityLogs.Dtos;

public record SmsAttemptDto
{
    public required int Id { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public int? UserId { get; init; }

    public string? UserName { get; init; }

    public string? Type { get; init; }

    public string? Status { get; init; }

    public required string Ip { get; init; }

    public required string UserAgent { get; init; }

    public required DateTime CreatedAt { get; init; }
}
