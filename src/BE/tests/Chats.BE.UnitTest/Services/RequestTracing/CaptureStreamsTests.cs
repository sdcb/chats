using Chats.BE.Services.RequestTracing;
using System.Text;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public class CaptureStreamsTests
{
    [Fact]
    public void WriteCaptureStream_ShouldCapturePrefixAndMarkTruncated()
    {
        byte[] payload = Encoding.UTF8.GetBytes("abcdef");
        using MemoryStream inner = new();
        using WriteCaptureStream tee = new(inner, 4);

        tee.Write(payload, 0, payload.Length);

        Assert.Equal(payload.Length, inner.ToArray().Length);
        Assert.Equal(Encoding.UTF8.GetBytes("abcd"), tee.CapturedBytes);
        Assert.True(tee.IsTruncated);
    }

    [Fact]
    public void WriteCaptureStream_ShouldUseDefaultLimitWhenNotProvided()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello");
        using MemoryStream inner = new();
        using WriteCaptureStream tee = new(inner);

        tee.Write(payload, 0, payload.Length);

        Assert.Equal(payload, tee.CapturedBytes);
        Assert.False(tee.IsTruncated);
    }

    [Fact]
    public void ReadCaptureStream_ShouldCaptureReadPrefixAndMarkTruncated()
    {
        byte[] payload = Encoding.UTF8.GetBytes("abcdef");
        using MemoryStream inner = new(payload);

        int totalBytesRead = 0;
        byte[]? capturedBytes = null;
        bool truncated = false;

        using ReadCaptureStream capture = new(
            inner,
            4,
            (total, captured, isTruncated) =>
            {
                totalBytesRead = total;
                capturedBytes = captured;
                truncated = isTruncated;
            });

        byte[] buffer = new byte[32];
        while (capture.Read(buffer, 0, buffer.Length) > 0)
        {
        }

        Assert.Equal(payload.Length, totalBytesRead);
        Assert.Equal(Encoding.UTF8.GetBytes("abcd"), capturedBytes);
        Assert.True(truncated);
    }
}
