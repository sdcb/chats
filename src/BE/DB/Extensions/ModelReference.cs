namespace Chats.BE.DB;

public partial class ModelReference
{
    public float? UnnormalizeTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return (float)Math.Clamp(temperature.Value, (float)MinTemperature, (float)MaxTemperature);
    }

    public static bool IsSdkUnsupportedO1(string modelReferenceName) => modelReferenceName switch
    {
        "o1-2024-12-17" => true,
        "o3-mini-2025-01-31" => true,
        "o3" => true,
        "o4-mini" => true,
        _ => false
    };

    public static bool SupportReasoningEffort(string modelReferenceName)
    {
        return modelReferenceName switch
        {
            "o1-2024-12-17" => true,
            "o3-mini-2025-01-31" => true,
            "grok-3-mini" => true,
            "grok-3-mini-fast" => true,
            "o3" => true,
            "o4-mini" => true,
            "gemini-2.5-pro-exp-03-25" => true,
            "gemini-2.5-flash-preview-04-17" => true, 
            _ => false
        };
    }
}
