namespace Chats.DB;

public partial class Model
{
    public float? ClampTemperature(float? temperature)
    {
        if (temperature == null) return null;
        return (float)Math.Clamp(temperature.Value, (float)CurrentSnapshot.MinTemperature, (float)CurrentSnapshot.MaxTemperature);
    }

    public string? ClampEffort(string? effort)
    {
        return ReasoningEfforts.Clamp(effort, CurrentSnapshot.SupportedEfforts);
    }

    public static string[] GetSupportedEffortsAsArray(string? supportedEffortsInDb)
    {
        return ReasoningEfforts.ParseSupportedEfforts(supportedEffortsInDb);
    }

    public static string[] GetSupportedImageSizesAsArray(string? supportedImageSizesInDB)
    {
        if (string.IsNullOrEmpty(supportedImageSizesInDB))
        {
            return [];
        }
        return supportedImageSizesInDB.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string[] GetSupportedFormatsAsArray(string? supportedFormatsInDb)
    {
        if (string.IsNullOrEmpty(supportedFormatsInDb))
        {
            return [];
        }

        return supportedFormatsInDb.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
