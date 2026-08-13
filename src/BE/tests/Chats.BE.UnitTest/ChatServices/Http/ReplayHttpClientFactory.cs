using System.Net;
using System.Text;
using System.Text.Json;

namespace Chats.BE.UnitTest.ChatServices.Http;

/// <summary>
/// HttpClientFactory test double that returns an inline response body and optionally validates
/// the outgoing request JSON.
/// </summary>
public sealed class ReplayHttpClientFactory : IHttpClientFactory
{
    private readonly string responseBody;
    private readonly HttpStatusCode statusCode;
    private readonly string? expectedRequestBody;

    public ReplayHttpClientFactory(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK, string? expectedRequestBody = null)
    {
        this.responseBody = responseBody;
        this.statusCode = statusCode;
        this.expectedRequestBody = expectedRequestBody;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new ReplayHttpMessageHandler(responseBody, statusCode, expectedRequestBody));
    }
}

internal sealed class ReplayHttpMessageHandler(string responseBody, HttpStatusCode statusCode, string? expectedRequestBody) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(expectedRequestBody))
        {
            string actualBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            JsonRequestAssertions.AssertSameJson(expectedRequestBody, actualBody);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}

/// <summary>
/// Compares two JSON payloads for both shape and values.
/// Note: string values are compared using JsonElement.GetString(), which naturally treats escaped and unescaped strings as equal.
/// </summary>
public static class JsonRequestAssertions
{
    public static void AssertSameJson(string expectedJson, string actualJson)
    {
        if (string.IsNullOrWhiteSpace(expectedJson))
        {
            throw new InvalidOperationException("Expected request JSON is missing.");
        }

        try
        {
            using JsonDocument expectedDoc = JsonDocument.Parse(expectedJson);
            using JsonDocument actualDoc = JsonDocument.Parse(actualJson);

            List<string> diffs = [];
            Compare(expectedDoc.RootElement, actualDoc.RootElement, "$", diffs);

            if (diffs.Count == 0)
            {
                return;
            }

            string details = string.Join("\n", diffs.Take(50));
            if (diffs.Count > 50)
            {
                details += $"\n... ({diffs.Count - 50} more)";
            }

            throw new InvalidOperationException($"Request JSON mismatch (shape and/or values).\n{details}");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Request JSON body is missing or not valid JSON.");
        }
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path, List<string> diffs)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            diffs.Add($"{path}: kind mismatch, expected {expected.ValueKind}, actual {actual.ValueKind}");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                HashSet<string> expectedNames = expected.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                HashSet<string> actualNames = actual.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

                foreach (string missing in expectedNames.Except(actualNames).OrderBy(x => x))
                {
                    diffs.Add($"{path}.{missing}: missing property");
                }
                foreach (string extra in actualNames.Except(expectedNames).OrderBy(x => x))
                {
                    diffs.Add($"{path}.{extra}: extra property");
                }

                foreach (string name in expectedNames.Intersect(actualNames).OrderBy(x => x))
                {
                    Compare(expected.GetProperty(name), actual.GetProperty(name), $"{path}.{name}", diffs);
                }
                break;
            }

            case JsonValueKind.Array:
            {
                int expectedLen = expected.GetArrayLength();
                int actualLen = actual.GetArrayLength();
                if (expectedLen != actualLen)
                {
                    diffs.Add($"{path}: array length mismatch, expected {expectedLen}, actual {actualLen}");
                }

                int len = Math.Min(expectedLen, actualLen);
                for (int i = 0; i < len; i++)
                {
                    Compare(expected[i], actual[i], $"{path}[{i}]", diffs);
                }
                break;
            }

            case JsonValueKind.String:
            {
                string? expectedStr = expected.GetString();
                string? actualStr = actual.GetString();
                if (!string.Equals(expectedStr, actualStr, StringComparison.Ordinal))
                {
                    diffs.Add($"{path}: string mismatch, expected={FormatString(expectedStr)}, actual={FormatString(actualStr)}");
                }
                break;
            }

            case JsonValueKind.Number:
            {
                if (expected.TryGetDecimal(out decimal expectedDec) && actual.TryGetDecimal(out decimal actualDec))
                {
                    if (expectedDec != actualDec)
                    {
                        diffs.Add($"{path}: number mismatch, expected={expectedDec}, actual={actualDec}");
                    }
                }
                else
                {
                    string expectedRaw = expected.GetRawText();
                    string actualRaw = actual.GetRawText();
                    if (!string.Equals(expectedRaw, actualRaw, StringComparison.Ordinal))
                    {
                        diffs.Add($"{path}: number mismatch, expected={expectedRaw}, actual={actualRaw}");
                    }
                }
                break;
            }

            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                if (expected.GetBoolean() != actual.GetBoolean())
                {
                    diffs.Add($"{path}: bool mismatch, expected={expected.GetBoolean()}, actual={actual.GetBoolean()}");
                }
                break;
            }

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;

            default:
            {
                string expectedRaw = expected.GetRawText();
                string actualRaw = actual.GetRawText();
                if (!string.Equals(expectedRaw, actualRaw, StringComparison.Ordinal))
                {
                    diffs.Add($"{path}: value mismatch, expected={expectedRaw}, actual={actualRaw}");
                }
                break;
            }
        }
    }

    private static string FormatString(string? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        const int maxLen = 200;
        if (value.Length <= maxLen)
        {
            return JsonSerializer.Serialize(value);
        }

        return JsonSerializer.Serialize(value[..maxLen] + "...");
    }
}
