using Chats.BE.DB.Enums;

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

    public static bool SupportsCodeExecution(string modelReferenceName) => modelReferenceName switch
    {
        "gemini-2.0-flash-lite" => false,
        "gemini-2.0-flash-exp" => false,
        "gemini-2.0-flash-exp-image-generation" => false,
        _ when modelReferenceName.StartsWith("gemini-") => true,
        _ => false
    };

    public static bool TryGetLowestSupportedReasoningEffort(string modelReferenceName, out DBReasoningEffort reasoningEffort)
    {
        DBReasoningEffort[] options = ReasoningEffortOptions(modelReferenceName);
        if (options.Length == 0)
        {
            reasoningEffort = DBReasoningEffort.Default;
            return false;
        }
        reasoningEffort = options.Min();
        return true;
    }

    public static DBReasoningEffort[] ReasoningEffortOptions(string modelReferenceName)
    {
        DBReasoningEffort[] TranditionalReasoning = [DBReasoningEffort.Low, DBReasoningEffort.Medium, DBReasoningEffort.High];
        DBReasoningEffort[] Gpt5Reasoning = [DBReasoningEffort.Minimal, DBReasoningEffort.Low, DBReasoningEffort.Medium, DBReasoningEffort.High];
        DBReasoningEffort[] Compatible = [DBReasoningEffort.Low];

        return modelReferenceName switch
        {
            "o1-2024-12-17" => TranditionalReasoning,
            "o3-mini-2025-01-31" => TranditionalReasoning,
            "grok-3-mini" => TranditionalReasoning,
            "grok-3-mini-fast" => TranditionalReasoning,
            "o3" => TranditionalReasoning,
            "o3-pro" => TranditionalReasoning,
            "o4-mini" => TranditionalReasoning,
            "codex-mini" => TranditionalReasoning,
            "gemini-2.5-pro" => Compatible,
            "gemini-2.5-flash" => Compatible,
            "gpt-image-1" => TranditionalReasoning,
            "Qwen/Qwen3-235B-A22B" => Compatible,
            "Qwen/Qwen3-30B-A3B" => Compatible,
            "Qwen/Qwen3-32B" => Compatible,
            "Qwen/Qwen3-14B" => Compatible,
            "Qwen/Qwen3-8B" => Compatible,
            "qwen3-235b-a22b" => Compatible,
            "qwen3-30b-a3b" => Compatible,
            "qwen3-32b" => Compatible,
            "qwen3-14b" => Compatible,
            "qwen3-8b" => Compatible,
            "qwen3-4b" => Compatible,
            "qwen3-1.7b" => Compatible,
            "qwen3-0.6b" => Compatible,
            "gpt-5" or "gpt-5-mini" or "gpt-5-nano" => Gpt5Reasoning,
            _ => []
        };
    }

    public static int[] ReasoningEffortOptionsAsInt32(string modelReferenceName) => [.. ReasoningEffortOptions(modelReferenceName).Select(e => (int)e)];
}
