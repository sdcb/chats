using Chats.BE.DB.Enums;

namespace Chats.BE.Services.Models.ChatServices;

public record ChatExtraDetails
{
    public short TimezoneOffset { get; init; }

    public bool WebSearchEnabled { get; init; }

    public bool CodeExecutionEnabled { get; init; }

    public DBReasoningEffort ReasoningEffort { get; init; }

    public string? ImageSize { get; init; }

    public DateTime Now => DateTime.UtcNow.AddMinutes(TimezoneOffset);

    public static ChatExtraDetails Default => new();
}
