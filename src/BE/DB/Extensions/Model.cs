using Chats.BE.DB.Enums;
using Chats.BE.DB.Jsons;

namespace Chats.BE.DB;

public partial class Model
{
    public DBApiType ApiType => (DBApiType)ApiTypeId;

    public JsonPriceConfig ToPriceConfig() => new()
    {
        InputTokenPrice = InputTokenPrice1M / 100_0000,
        OutputTokenPrice = OutputTokenPrice1M / 100_0000
    };

    public float? ClampTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return (float)Math.Clamp(temperature.Value, (float)MinTemperature, (float)MaxTemperature);
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

    internal byte ClampReasoningEffortId(byte reasoningEffortId)
    {
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
}