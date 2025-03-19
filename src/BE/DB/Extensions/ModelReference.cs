﻿namespace Chats.BE.DB;

public partial class ModelReference
{
    public float? UnnormalizeTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return temperature * (float)(MaxTemperature - MinTemperature) + (float)MinTemperature;
    }

    public bool IsSdkUnsupportedO1 => SupportReasoningEffort(Name);

    public static bool SupportReasoningEffort(string modelReferenceName)
    {
        return modelReferenceName switch
        {
            "o1-2024-12-17" => true,
            "o3-mini-2025-01-31" => true,
            _ => false
        };
    }
}
