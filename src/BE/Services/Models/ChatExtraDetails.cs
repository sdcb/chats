namespace Chats.BE.Services.Models;

public record ChatExtraDetails
{
    public short TimezoneOffset { get; init; }

    public bool WebSearchEnabled { get; init; }

    public DateTime Now => DateTime.UtcNow.AddMinutes(TimezoneOffset);

    public static ChatExtraDetails Default => new();
}
