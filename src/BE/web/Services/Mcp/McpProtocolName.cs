using System.Text.RegularExpressions;

namespace Chats.BE.Services.Mcp;

public static partial class McpProtocolName
{
    public const string Prefix = "mcp__";

    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$", RegexOptions.CultureInvariant)]
    private static partial Regex ServerNameRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNameRegex();

    public static bool IsValidServerName(string name) => ServerNameRegex().IsMatch(name);

    public static string BuildExposedToolName(string serverName, string toolName)
        => $"{Prefix}{serverName}__{toolName}";

    public static bool IsValidExposedToolName(string name) => ToolNameRegex().IsMatch(name);

    public static string? ValidateTools(string serverName, IEnumerable<string> toolNames)
    {
        foreach (string toolName in toolNames)
        {
            string exposedName = BuildExposedToolName(serverName, toolName);
            if (!IsValidExposedToolName(exposedName))
            {
                return $"Tool '{toolName}' produces invalid protocol name '{exposedName}'. " +
                    "The complete name must match ^[A-Za-z0-9_-]{1,64}$.";
            }
        }

        return null;
    }
}
