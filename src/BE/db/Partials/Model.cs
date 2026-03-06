using Chats.DB.Enums;

namespace Chats.DB;

public partial class Model
{
    public DBApiType ApiType => (DBApiType)ApiTypeId;

    public float? ClampTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return (float)Math.Clamp(temperature.Value, (float)MinTemperature, (float)MaxTemperature);
    }

    public byte ClampReasoningEffortId(byte reasoningEffortId)
    {
        // don't clamp if reasoningEffortId is 0, which indicates that reasoning effort is not specified and the system should use the default
        if (reasoningEffortId == 0) return 0;

        byte[] options = [.. GetReasoningEffortOptionsAsInt32(ReasoningEffortOptions).Select(x => (byte)x)];

        if (options.Length == 0)
        {
            return 0;
        }

        if (options.Contains(reasoningEffortId))
        {
            return reasoningEffortId;
        }

        return options[0];
    }

    public static int[] GetReasoningEffortOptionsAsInt32(string? reasoningEffortOptionsInDB)
    {
        if (string.IsNullOrEmpty(reasoningEffortOptionsInDB))
        {
            return [];
        }
        return [.. reasoningEffortOptionsInDB.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)];
    }

    public static string[] GetSupportedImageSizesAsArray(string? supportedImageSizesInDB)
    {
        if (string.IsNullOrEmpty(supportedImageSizesInDB))
        {
            return [];
        }
        return supportedImageSizesInDB.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
