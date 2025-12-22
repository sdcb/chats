namespace Chats.Web.DB.Enums;

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
    public static string? ToReasoningEffortString(this DBReasoningEffort effort) => effort switch
    {
        DBReasoningEffort.Default => null,
        DBReasoningEffort.Minimal => "minimal",
        DBReasoningEffort.Low => "low",
        DBReasoningEffort.Medium => "medium",
        DBReasoningEffort.High => "high",
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

    public static DBReasoningEffort FromString(string? effort)
    {
        if (string.IsNullOrEmpty(effort))
        {
            return DBReasoningEffort.Default;
        }
        return effort.ToLowerInvariant() switch
        {
            "minimal" => DBReasoningEffort.Minimal,
            "low" => DBReasoningEffort.Low,
            "medium" => DBReasoningEffort.Medium,
            "high" => DBReasoningEffort.High,
            _ => throw new Exception($"Unknown reasoning effort value: {effort}"),
        };
    }
}
