using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Buffers.Text;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;

namespace Chats.BE.Services;

public static class EtagCacheHelper
{
    public static bool TryHandleNotModified(ControllerBase controller, string resourceName, object? responseBody)
    {
        ValidateResourceName(resourceName);

        XxHash128 hasher = new();
        hasher.Append(Encoding.UTF8.GetBytes(resourceName));
        hasher.Append([(byte)':']);

        using HashWriteStream hashWriteStream = new(hasher);
        JsonSerializer.Serialize(hashWriteStream, responseBody, JSON.EtagJsonSerializerOptions);
        Span<byte> hash = stackalloc byte[hasher.HashLengthInBytes];
        hasher.GetHashAndReset(hash);
        string etagText = $"\"{resourceName}:{Base64Url.EncodeToString(hash)}\"";
        EntityTagHeaderValue etag = new(etagText, isWeak: false);

        controller.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            NoCache = true,
            MustRevalidate = true,
            MaxAge = TimeSpan.FromDays(30),
        };
        controller.Response.GetTypedHeaders().ETag = etag;

        return controller.Request.GetTypedHeaders().IfNoneMatch?.Any(x => x.Tag.Equals(etag.Tag, StringComparison.Ordinal)) == true;
    }

    private static void ValidateResourceName(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException("Resource name is required.", nameof(resourceName));
        }

        bool isValid = resourceName.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');
        if (!isValid || resourceName.StartsWith('-') || resourceName.EndsWith('-') || resourceName.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Resource name must be lowercase kebab-case.", nameof(resourceName));
        }
    }

    private sealed class HashWriteStream(NonCryptographicHashAlgorithm hash) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return;
            }

            hash.Append(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            hash.Append(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
    }
}