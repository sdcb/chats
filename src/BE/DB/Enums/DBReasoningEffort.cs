using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Responses;

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
    public static ChatReasoningEffortLevel? ToChatCompletionReasoningEffort(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => null,
        DBReasoningEffort.Minimal => ChatReasoningEffortLevel.Minimal,
        DBReasoningEffort.Low =>  ChatReasoningEffortLevel.Low,
        DBReasoningEffort.Medium => ChatReasoningEffortLevel.Medium,
        DBReasoningEffort.High => ChatReasoningEffortLevel.High,
        _ => throw new Exception($"Unknown DBReasoningEffort value: {effort}"),
    };

    public static ResponseReasoningEffortLevel? ToResponseReasoningEffort(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => new ResponseReasoningEffortLevel?(),
        DBReasoningEffort.Minimal => ResponseReasoningEffortLevel.Minimal,
        DBReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
        DBReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
        DBReasoningEffort.High => ResponseReasoningEffortLevel.High,
        _ => throw new Exception($"Unknown DBReasoningEffort value: {effort}"),
    };

    public static GeneratedImageQuality ToGeneratedImageQuality(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => GeneratedImageQuality.Auto,
        DBReasoningEffort.Minimal => GeneratedImageQuality.Low, // treating minimal as low for image quality
        DBReasoningEffort.Low => GeneratedImageQuality.Low,
        DBReasoningEffort.Medium => GeneratedImageQuality.Medium,
        DBReasoningEffort.High => new GeneratedImageQuality("high"),
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

public static class ReasoningEffortLevelExtensions
{
    public static DBReasoningEffort ToDBReasoningEffort(this ChatReasoningEffortLevel? effort)
    {
        if (effort == null)
        {
            return DBReasoningEffort.Default;
        }
        return effort.Value.ToString() switch
        {
            "minimal" => DBReasoningEffort.Minimal,
            "low" => DBReasoningEffort.Low,
            "medium" => DBReasoningEffort.Medium,
            "high" => DBReasoningEffort.High,
            _ => throw new Exception($"Unknown ChatReasoningEffortLevel value: {effort}"),
        };
    }
}
