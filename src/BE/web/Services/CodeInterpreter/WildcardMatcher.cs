using System.Text.RegularExpressions;

namespace Chats.BE.Services.CodeInterpreter;

internal static class WildcardMatcher
{
    internal static bool IsMatch(string pattern, string text)
    {
        if (pattern == "*") return true;
        if (string.IsNullOrEmpty(pattern)) return false;
        string regex = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(text, regex, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
