namespace Chats.DB;

public static class ReasoningEfforts
{
    public const string Minimal = "minimal";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string XHigh = "xhigh";
    public const string Max = "max";

    private static readonly HashSet<string> AllowedValues =
    [
        Minimal,
        Low,
        Medium,
        High,
        XHigh,
        Max,
    ];

    public static void ThrowIfInvalid(string? effort)
    {
        if (effort is null)
        {
            return;
        }

        if (!AllowedValues.Contains(effort))
        {
            throw new InvalidOperationException($"Unknown reasoning effort value: {effort}");
        }
    }

    public static bool IsLowOrMinimal(string? effort)
    {
        return effort == Minimal || effort == Low;
    }

    public static string[] ParseSupportedEfforts(string? supportedEfforts)
    {
        if (string.IsNullOrEmpty(supportedEfforts))
        {
            return [];
        }

        string[] efforts = supportedEfforts.Split(',');
        foreach (string effort in efforts)
        {
            ThrowIfInvalid(effort);
        }

        return efforts;
    }

    public static string? Clamp(string? effort, string? supportedEfforts)
    {
        if (effort is null)
        {
            return null;
        }

        ThrowIfInvalid(effort);

        string[] options = ParseSupportedEfforts(supportedEfforts);
        if (options.Length == 0)
        {
            return null;
        }

        if (options.Contains(effort, StringComparer.Ordinal))
        {
            return effort;
        }

        return options[0];
    }
}