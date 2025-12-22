using System.ClientModel;
using System.Globalization;
using System.Net.Http.Headers;

namespace Chats.Web.Services.Models.ChatServices.OpenAI.Special;

// from: https://github.com/openai/openai-dotnet/blob/06191fad53746b841badfac8f16cdd301a500094/src/Generated/Internal/MultiPartFormDataBinaryContent.cs#L16
internal partial class MultiPartFormDataBinaryContent : BinaryContent
{
    private readonly MultipartFormDataContent _multipartContent;
    private static readonly Random _random = new();
    private static readonly char[] _boundaryValues = "0123456789=ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz".ToCharArray();

    public MultiPartFormDataBinaryContent()
    {
        _multipartContent = new MultipartFormDataContent(CreateBoundary());
    }

    public string ContentType
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_multipartContent.Headers.ContentType);
            return _multipartContent.Headers.ContentType.ToString();
        }
    }

    internal HttpContent HttpContent => _multipartContent;

    private static string CreateBoundary()
    {
        Span<char> chars = new char[70];
        byte[] random = new byte[70];
        _random.NextBytes(random);
        int mask = 255 >> 2;
        int i = 0;
        for (; i < 70; i++)
        {
            chars[i] = _boundaryValues[random[i] & mask];
        }
        return chars.ToString();
    }

    public void Add(string content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentNullException.ThrowIfNull(content, nameof(content));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        Add(new StringContent(content), name, filename, contentType);
    }

    public void Add(int content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content.ToString("G", CultureInfo.InvariantCulture);
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(long content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content.ToString("G", CultureInfo.InvariantCulture);
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(float content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content.ToString("G", CultureInfo.InvariantCulture);
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(double content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content.ToString("G", CultureInfo.InvariantCulture);
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(decimal content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content.ToString("G", CultureInfo.InvariantCulture);
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(bool content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        string value = content ? "true" : "false";
        Add(new StringContent(value), name, filename, contentType);
    }

    public void Add(Stream content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentNullException.ThrowIfNull(content, nameof(content));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        Add(new StreamContent(content), name, filename, contentType);
    }

    public void Add(byte[] content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentNullException.ThrowIfNull(content, nameof(content));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        Add(new ByteArrayContent(content), name, filename, contentType);
    }

    public void Add(BinaryData content, string name, string? filename = default, string? contentType = default)
    {
        ArgumentNullException.ThrowIfNull(content, nameof(content));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        Add(new ByteArrayContent(content.ToArray()), name, filename, contentType);
    }

    private void Add(HttpContent content, string name, string? filename, string? contentType)
    {
        if (contentType != null)
        {
            ArgumentException.ThrowIfNullOrEmpty(contentType, nameof(contentType));
            AddContentTypeHeader(content, contentType);
        }
        if (filename != null)
        {
            ArgumentException.ThrowIfNullOrEmpty(filename, nameof(filename));
            _multipartContent.Add(content, name, filename);
        }
        else
        {
            _multipartContent.Add(content, name);
        }
    }

    public static void AddContentTypeHeader(HttpContent content, string contentType)
    {
        MediaTypeHeaderValue header = new MediaTypeHeaderValue(contentType);
        content.Headers.ContentType = header;
    }

    public override bool TryComputeLength(out long length)
    {
        if (_multipartContent.Headers.ContentLength is long contentLength)
        {
            length = contentLength;
            return true;
        }
        length = 0;
        return false;
    }

    public override void WriteTo(Stream stream, CancellationToken cancellationToken = default)
    {
        _multipartContent.CopyTo(stream, default, cancellationToken);
    }

    public override async Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await _multipartContent.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _multipartContent.Dispose();
    }
}