using OpenAI.Chat;
using OpenAI.Images;

namespace Chats.BE.DB.Enums;

public enum DBReasoningEffort : byte
{
    Default = 0, 
    Minimal = 1,
    Low = 2,
    Medium = 3,
    High = 4,
}

public static class DBReasoningEffortExtensions
{
    public static ChatReasoningEffortLevel? ToReasoningEffort(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => null,
        DBReasoningEffort.Minimal => new ChatReasoningEffortLevel("minimal"),
        DBReasoningEffort.Low =>  ChatReasoningEffortLevel.Low,
        DBReasoningEffort.Medium => ChatReasoningEffortLevel.Medium,
        DBReasoningEffort.High => ChatReasoningEffortLevel.High,
        _ => throw new Exception($"Unknown DBReasoningEffort value: {effort}"),
    };

    public static GeneratedImageQuality ToGeneratedImageQuality(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => GeneratedImageQuality.Auto,
        DBReasoningEffort.Minimal => GeneratedImageQuality.Low, // treating minimal as low for image quality
        DBReasoningEffort.Low => GeneratedImageQuality.Low,
        DBReasoningEffort.Medium => GeneratedImageQuality.Medium,
        DBReasoningEffort.High => GeneratedImageQuality.High,
        _ => throw new Exception($"Unknown DBReasoningEffort value: {effort}"),
    };

    public static string? ToGeneratedImageQualityText(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => null,
        DBReasoningEffort.Minimal => "low", // treating minimal as low for image quality
        DBReasoningEffort.Low => "low",
        DBReasoningEffort.Medium => "medium",
        DBReasoningEffort.High => "high",
        _ => throw new Exception($"Unknown DBReasoningEffort value: {effort}"),
    };

    public static bool IsLowOrMinimal(this DBReasoningEffort effort) => effort == DBReasoningEffort.Low || effort == DBReasoningEffort.Minimal;
}
