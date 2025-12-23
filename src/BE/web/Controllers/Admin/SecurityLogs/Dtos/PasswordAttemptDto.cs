namespace Chats.BE.Controllers.Admin.SecurityLogs.Dtos;

public record PasswordAttemptDto
{
    public required int Id { get; init; }

    public required string UserName { get; init; }

    public int? UserId { get; init; }

    public string? MatchedUserName { get; init; }

    public required bool IsSuccessful { get; init; }

    public string? FailureReason { get; init; }

    public required string Ip { get; init; }

    public required string UserAgent { get; init; }

    public required DateTime CreatedAt { get; init; }
}
