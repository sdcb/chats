namespace Chats.BE.DB;

public partial class ModelReference
{
    public float? UnnormalizeTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return (float)Math.Clamp(temperature.Value, (float)MinTemperature, (float)MaxTemperature);
    }

    public static bool SupportsDeveloperMessage(string modelReferenceName) => modelReferenceName switch
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
            "o3-pro" => true,
            "o4-mini" => true,
            "codex-mini" => true,
            "gemini-2.5-pro" => true,
            "gemini-2.5-flash" => true, 
            "gpt-image-1" => true, 
            "Qwen/Qwen3-235B-A22B" => true,
            "Qwen/Qwen3-30B-A3B" => true,
            "Qwen/Qwen3-32B" => true,
            "Qwen/Qwen3-14B" => true,
            "Qwen/Qwen3-8B" => true,
            "qwen3-235b-a22b" => true,
            "qwen3-30b-a3b" => true,
            "qwen3-32b" => true,
            "qwen3-14b" => true,
            "qwen3-8b" => true,
            "qwen3-4b" => true,
            "qwen3-1.7b" => true,
            "qwen3-0.6b" => true,
            "gpt-5" or "gpt-5-mini" or "gpt-5-nano" => true,
            _ => false
        };
    }
}
