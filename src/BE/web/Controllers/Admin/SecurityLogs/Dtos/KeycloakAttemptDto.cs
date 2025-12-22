namespace Chats.Web.Controllers.Admin.SecurityLogs.Dtos;

public record KeycloakAttemptDto
{
    public required int Id { get; init; }

    public required string Provider { get; init; }

    public string? Sub { get; init; }

    public string? Email { get; init; }

    public int? UserId { get; init; }

    public string? UserName { get; init; }

    public required bool IsSuccessful { get; init; }

    public string? FailureReason { get; init; }

    public required string Ip { get; init; }

    public required string UserAgent { get; init; }

    public required DateTime CreatedAt { get; init; }
}
