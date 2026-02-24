using Chats.BE.Services.RequestTracing;
using System.Text;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public class ObservedHttpContentTests
{
    [Fact]
    public async Task ObservedRequestHttpContent_ShouldInvokeCallback_WhenSerialized()
    {
        byte[] payload = Encoding.UTF8.GetBytes("abcdef");
        using ByteArrayContent inner = new(payload);

        int totalBytes = -1;
        byte[]? capturedBytes = null;
        bool truncated = false;

        using ObservedRequestHttpContent observed = new(
            inner,
            4,
            (total, captured, isTruncated) =>
            {
                totalBytes = total;
                capturedBytes = captured;
                truncated = isTruncated;
            });

        using MemoryStream sink = new();
        await observed.CopyToAsync(sink);

        Assert.Equal(payload.Length, totalBytes);
        Assert.Equal(Encoding.UTF8.GetBytes("abcd"), capturedBytes);
        Assert.True(truncated);
    }

    [Fact]
    public async Task ObservedResponseHttpContent_ShouldInvokeCallback_WhenReadToEnd()
    {
        byte[] payload = Encoding.UTF8.GetBytes("abcdef");
        using ByteArrayContent inner = new(payload);

        int totalBytes = -1;
        byte[]? capturedBytes = null;
        bool truncated = false;

        using ObservedResponseHttpContent observed = new(
            inner,
            4,
            (total, captured, isTruncated) =>
            {
                totalBytes = total;
                capturedBytes = captured;
                truncated = isTruncated;
            });

        await using Stream stream = await observed.ReadAsStreamAsync();
        byte[] buffer = new byte[16];
        while (await stream.ReadAsync(buffer, 0, buffer.Length) > 0)
        {
        }

        Assert.Equal(payload.Length, totalBytes);
        Assert.Equal(Encoding.UTF8.GetBytes("abcd"), capturedBytes);
        Assert.True(truncated);
    }

    [Fact]
    public void ObservedResponseHttpContent_ShouldNotInvokeCallback_WhenNotConsumed()
    {
        byte[] payload = Encoding.UTF8.GetBytes("abcdef");
        using ByteArrayContent inner = new(payload);

        bool invoked = false;
        using ObservedResponseHttpContent observed = new(
            inner,
            4,
            (_, _, _) => invoked = true);

        Assert.False(invoked);
    }
}
