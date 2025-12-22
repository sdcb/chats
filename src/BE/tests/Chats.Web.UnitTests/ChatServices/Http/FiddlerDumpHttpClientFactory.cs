using System.Net;
using System.Text;
using System.Text.Json;

namespace Chats.Web.Tests.ChatServices.Http;

/// <summary>
/// HttpClientFactory test double that replays a chunked response captured from a Fiddler dump,
/// and optionally validates the outgoing request JSON against the captured request body.
/// </summary>
public sealed class FiddlerDumpHttpClientFactory : IHttpClientFactory
{
    private readonly List<string> chunks;
    private readonly HttpStatusCode statusCode;
    private readonly string? expectedRequestBody;

    public FiddlerDumpHttpClientFactory(List<string> chunks, HttpStatusCode statusCode = HttpStatusCode.OK, string? expectedRequestBody = null)
    {
        this.chunks = chunks;
        this.statusCode = statusCode;
        this.expectedRequestBody = expectedRequestBody;
    }

    public HttpClient CreateClient(string name)
    {
        var handler = new FiddlerDumpHttpMessageHandler(chunks, statusCode, expectedRequestBody);
        return new HttpClient(handler);
    }
}

internal sealed class FiddlerDumpHttpMessageHandler : HttpMessageHandler
{
    private readonly List<string> chunks;
    private readonly HttpStatusCode statusCode;
    private readonly string? expectedRequestBody;

    public FiddlerDumpHttpMessageHandler(List<string> chunks, HttpStatusCode statusCode = HttpStatusCode.OK, string? expectedRequestBody = null)
    {
        this.chunks = chunks;
        this.statusCode = statusCode;
        this.expectedRequestBody = expectedRequestBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(expectedRequestBody))
        {
            string actualBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            JsonRequestAssertions.AssertSameJson(expectedRequestBody, actualBody);
        }

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StreamContent(new ChunkedMemoryStream(chunks))
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        {
            CharSet = "UTF-8"
        };
        return response;
    }
}

/// <summary>
/// Simulates chunked stream response. Converts a list of chunks into a readable stream.
/// Now that FiddlerHttpDumpParser correctly parses HTTP chunks, we just need to concatenate them.
/// </summary>
public sealed class ChunkedMemoryStream : Stream
{
    private readonly MemoryStream innerStream;

    public ChunkedMemoryStream(List<string> chunks)
    {
        // Chunks 现在已经是正确解析的内容，直接拼接即可
        var content = string.Concat(chunks);
        var bytes = Encoding.UTF8.GetBytes(content);
        innerStream = new MemoryStream(bytes);
    }

    public override bool CanRead => true;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => innerStream.Length;
    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush() => innerStream.Flush();
    public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
        }
        base.Dispose(disposing);
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
