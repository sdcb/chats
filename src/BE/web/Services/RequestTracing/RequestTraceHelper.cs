using Chats.BE.Services.Configs;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Chats.BE.Services.RequestTracing;

public static partial class RequestTraceHelper
{
    public const int DefaultRawCaptureMaxBytes = 20 * 1024 * 1024;

    private const string TruncateMessageTemplate = "... [content truncated: {0} chars omitted] ...";

    public static bool IsEnabledAndSampled(RequestTraceConfig config)
    {
        if (!config.Enabled) return false;
        double sampleRate = Math.Clamp(config.SampleRate, 0, 1);
        if (sampleRate <= 0) return false;
        if (sampleRate >= 1) return true;
        return Random.Shared.NextDouble() <= sampleRate;
    }

    public static bool MatchFilters(RequestTraceFilters filters, string? source, string method, string url, short? statusCode, int durationMs)
    {
        if (!MatchesPatterns(source, filters.SourcePatterns)) return false;
        if (!MatchesPatterns(url, filters.IncludeUrlPatterns)) return false;
        if (MatchesPatterns(url, filters.ExcludeUrlPatterns, emptyPatternsResult: false)) return false;

        if (filters.Methods is { Length: > 0 } && !filters.Methods.Any(x => string.Equals(x, method, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (filters.StatusCodes is { Length: > 0 })
        {
            if (!statusCode.HasValue) return false;
            if (!MatchStatus(statusCode.Value, filters.StatusCodes)) return false;
        }

        if (filters.MinDurationMs.HasValue && durationMs < filters.MinDurationMs.Value)
        {
            return false;
        }

        return true;
    }

    public static string FormatHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
        string[]? includes,
        string[]? redactHeaders)
    {
        HashSet<string>? includeSet = includes == null ? null : includes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? redactSet = redactHeaders == null ? null : redactHeaders.ToHashSet(StringComparer.OrdinalIgnoreCase);

        StringBuilder builder = new();
        foreach ((string name, IEnumerable<string> values) in headers)
        {
            if (includeSet != null && !includeSet.Contains(name))
            {
                continue;
            }

            bool redact = redactSet?.Contains(name) == true;
            string value = redact ? "***" : string.Join(",", values);
            builder.Append(name).Append(": ").Append(value).Append('\n');
        }

        return builder.ToString();
    }

    public static (string? text, int? originalLength) DecodeTextBody(
        byte[]? source,
        int maxTextChars,
        string? contentEncoding,
        string[]? allowedContentTypes,
        string? contentType)
    {
        if (source == null) return (null, null);
        if (source.Length == 0) return (null, 0);
        if (!IsAllowedContentType(contentType, allowedContentTypes)) return (null, null);

        byte[] bodyBytes = source;
        if (!string.IsNullOrWhiteSpace(contentEncoding))
        {
            bodyBytes = TryDecompress(source, contentEncoding) ?? source;
        }

        Encoding encoding = ResolveEncoding(contentType);
        string text = encoding.GetString(bodyBytes);
        int originalLength = text.Length;
        if (text.Length <= maxTextChars)
        {
            return (text, originalLength);
        }

        return (TruncateMiddle(text, maxTextChars), originalLength);
    }

    public static bool IsAllowedContentType(string? contentType, string[]? allowedContentTypes)
    {
        if (allowedContentTypes == null || allowedContentTypes.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return allowedContentTypes.Any(pattern => MatchWildcard(contentType, pattern));
    }

    public static bool MatchRequestStageFilters(RequestTraceFilters filters, string? source, string method, string url)
    {
        if (!MatchesPatterns(source, filters.SourcePatterns)) return false;
        if (!MatchesPatterns(url, filters.IncludeUrlPatterns)) return false;
        if (MatchesPatterns(url, filters.ExcludeUrlPatterns, emptyPatternsResult: false)) return false;
        if (filters.Methods is { Length: > 0 } && !filters.Methods.Any(x => string.Equals(x, method, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    public static bool MatchResponseStageFilters(RequestTraceFilters filters, string? source, string method, string url, short? statusCode, int durationMs)
        => MatchFilters(filters, source, method, url, statusCode, durationMs);

    public static int ResolveRawCaptureLimit(int? configured)
    {
        if (configured.HasValue && configured.Value > 0)
        {
            return configured.Value;
        }

        return DefaultRawCaptureMaxBytes;
    }

    public static DateTime? ResolveScheduledDeleteAt(DateTime startedAtUtc, int? retentionDays)
    {
        if (retentionDays is > 0)
        {
            return startedAtUtc.AddDays(retentionDays.Value);
        }

        return null;
    }

    public static bool IsSmallKnownLength(long? contentLength, int maxCaptureBytes)
    {
        if (!contentLength.HasValue) return false;
        if (contentLength.Value < 0) return false;
        long cap = Math.Max(maxCaptureBytes, 1024 * 256);
        return contentLength.Value <= cap;
    }

    private static bool MatchStatus(short code, string[] patterns)
    {
        foreach (string pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            if (short.TryParse(pattern, out short exact) && exact == code)
            {
                return true;
            }

            string p = pattern.Trim();
            if (p.Length == 3 && p[1] == 'x' && p[2] == 'x' && char.IsDigit(p[0]))
            {
                int group = p[0] - '0';
                if (code / 100 == group) return true;
            }
        }

        return false;
    }

    private static bool MatchesPatterns(string? value, string[]? patterns, bool emptyPatternsResult = true)
    {
        if (patterns == null || patterns.Length == 0)
        {
            return emptyPatternsResult;
        }

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return patterns.Any(pattern => MatchWildcard(value, pattern));
    }

    private static bool MatchWildcard(string value, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        if (pattern == "*") return true;

        StringBuilder regexBuilder = new("^");
        foreach (char c in pattern)
        {
            _ = c switch
            {
                '*' => regexBuilder.Append(".*"),
                '?' => regexBuilder.Append('.'),
                _ => regexBuilder.Append(Regex.Escape(c.ToString())),
            };
        }
        regexBuilder.Append('$');
        string regexText = regexBuilder.ToString();
        return Regex.IsMatch(value, regexText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static Encoding ResolveEncoding(string? contentType)
    {
        string? charset = ExtractCharset(contentType);
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"', '\''));
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static string TruncateMiddle(string text, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maxChars)
        {
            return text;
        }

        int omitted = text.Length - maxChars;
        string marker = string.Format(TruncateMessageTemplate, omitted);
        if (marker.Length >= maxChars)
        {
            return text[..maxChars];
        }

        int remain = maxChars - marker.Length;
        int head = remain / 2;
        int tail = remain - head;
        return text[..head] + marker + text[^tail..];
    }

    private static string? ExtractCharset(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;

        Match match = CharsetRegex().Match(contentType);
        if (!match.Success) return null;
        return match.Groups["charset"].Value;
    }

    private static byte[]? TryDecompress(byte[] source, string contentEncoding)
    {
        try
        {
            using MemoryStream input = new(source);
            using Stream decoder = contentEncoding.Trim().ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(input, CompressionMode.Decompress, leaveOpen: false),
                "br" => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: false),
                "deflate" => new DeflateStream(input, CompressionMode.Decompress, leaveOpen: false),
                _ => throw new NotSupportedException(),
            };
            using MemoryStream output = new();
            decoder.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"charset\s*=\s*(?<charset>[^;\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CharsetRegex();
}
