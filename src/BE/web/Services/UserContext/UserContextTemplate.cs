using System.Globalization;
using System.Text;

namespace Chats.BE.Services.UserContext;

public sealed record UserContextContribution(
    string Key,
    string Content,
    IReadOnlyCollection<byte>? SpanIds = null);

public static class UserContextTemplate
{
    public const string UserContentPlaceholder = "{{USER_CONTENT}}";

    private const string SpanBlockPrefix = "{{#spans:";
    private const string SpanBlockSuffix = "}}";
    private const string SpanBlockEnd = "{{/spans}}";

    public static string Build(
        DateTimeOffset currentTime,
        IEnumerable<UserContextContribution>? contributions = null)
    {
        List<NormalizedContribution> normalized = NormalizeContributions(contributions ?? []);

        StringBuilder sb = new();
        sb.Append("<context>\n");
        sb.Append("<current_time>")
            .Append(currentTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))
            .Append("</current_time>\n");

        foreach (NormalizedContribution contribution in normalized)
        {
            if (contribution.SpanIds is { Count: > 0 })
            {
                sb.Append(SpanBlockPrefix)
                    .AppendJoin(',', contribution.SpanIds)
                    .Append(SpanBlockSuffix)
                    .Append('\n');
            }

            sb.Append('<').Append(contribution.Key).Append(">\n");
            sb.Append(contribution.Content).Append('\n');
            sb.Append("</").Append(contribution.Key).Append(">\n");

            if (contribution.SpanIds is { Count: > 0 })
            {
                sb.Append(SpanBlockEnd).Append('\n');
            }
        }

        sb.Append("</context>\n");
        sb.Append("<user_request>").Append(UserContentPlaceholder).Append("</user_request>");
        return sb.ToString();
    }

    public static string Render(string? contextTemplate, string content, byte spanId)
    {
        if (contextTemplate is null)
        {
            return content;
        }

        string template = NormalizeLineEndings(contextTemplate);
        if (CountOccurrences(template, UserContentPlaceholder) != 1)
        {
            throw new InvalidOperationException($"Context template must contain exactly one {UserContentPlaceholder} placeholder.");
        }

        string[] lines = template.Split('\n');
        List<string> output = new(lines.Length);
        bool insideSpanBlock = false;
        bool includeCurrentBlock = false;

        foreach (string line in lines)
        {
            string directive = line.Trim();
            if (TryParseSpanBlockStart(directive, out HashSet<byte>? spanIds))
            {
                if (insideSpanBlock)
                {
                    throw new InvalidOperationException("Nested span blocks are not supported in context templates.");
                }

                insideSpanBlock = true;
                includeCurrentBlock = spanIds.Contains(spanId);
                continue;
            }

            if (directive == SpanBlockEnd)
            {
                if (!insideSpanBlock)
                {
                    throw new InvalidOperationException("Context template contains an unmatched span block end.");
                }

                insideSpanBlock = false;
                includeCurrentBlock = false;
                continue;
            }

            if (!insideSpanBlock || includeCurrentBlock)
            {
                output.Add(line);
            }
        }

        if (insideSpanBlock)
        {
            throw new InvalidOperationException("Context template contains an unclosed span block.");
        }

        string rendered = string.Join('\n', output);
        return rendered.Replace(UserContentPlaceholder, content, StringComparison.Ordinal);
    }

    private static List<NormalizedContribution> NormalizeContributions(IEnumerable<UserContextContribution> contributions)
    {
        Dictionary<(string Key, string Content), ContributionGroup> groups = [];

        foreach (UserContextContribution contribution in contributions)
        {
            string key = contribution.Key.Trim();
            ValidateKey(key);

            string content = NormalizeLineEndings(contribution.Content).Trim('\n');
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            (string Key, string Content) groupKey = (key, content);
            if (!groups.TryGetValue(groupKey, out ContributionGroup? group))
            {
                group = new ContributionGroup();
                groups.Add(groupKey, group);
            }

            if (contribution.SpanIds is null)
            {
                group.IsGlobal = true;
                group.SpanIds.Clear();
                continue;
            }

            if (!group.IsGlobal)
            {
                group.SpanIds.UnionWith(contribution.SpanIds);
            }
        }

        return [.. groups
            .Where(x => x.Value.IsGlobal || x.Value.SpanIds.Count > 0)
            .OrderBy(x => x.Key.Key, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Content, StringComparer.Ordinal)
            .Select(x => new NormalizedContribution(
                x.Key.Key,
                x.Key.Content,
                x.Value.IsGlobal ? null : [.. x.Value.SpanIds.Order()]))];
    }

    private static bool TryParseSpanBlockStart(string directive, out HashSet<byte> spanIds)
    {
        spanIds = [];
        if (!directive.StartsWith(SpanBlockPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!directive.EndsWith(SpanBlockSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid span block start in context template.");
        }

        string idsText = directive[SpanBlockPrefix.Length..^SpanBlockSuffix.Length];
        if (string.IsNullOrWhiteSpace(idsText))
        {
            throw new InvalidOperationException("Span block must contain at least one span id.");
        }

        foreach (string token in idsText.Split(','))
        {
            string trimmed = token.Trim();
            if (!byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out byte spanId))
            {
                throw new InvalidOperationException($"Invalid span id '{trimmed}' in context template.");
            }

            if (!spanIds.Add(spanId))
            {
                throw new InvalidOperationException($"Duplicate span id '{spanId}' in context template.");
            }
        }

        return true;
    }

    private static void ValidateKey(string key)
    {
        if (key.Length == 0
            || !(char.IsAsciiLetter(key[0]) || key[0] == '_')
            || !key.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException("Context contribution keys may only contain ASCII letters, digits, and underscores.", nameof(key));
        }
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }

    private sealed class ContributionGroup
    {
        public bool IsGlobal { get; set; }
        public HashSet<byte> SpanIds { get; } = [];
    }

    private sealed record NormalizedContribution(
        string Key,
        string Content,
        IReadOnlyList<byte>? SpanIds);
}
