using Chats.BE.Services.FileServices;
using Chats.DB;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services;

public class FileImageInfoServiceTests
{
    private readonly FileImageInfoService _service = new(NullLogger<FileImageInfoService>.Instance);

    private static readonly byte[] ValidPngBytes =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] ValidJpegBytes =
        Convert.FromBase64String("/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

    // WebP extended format (VP8X)
    private static readonly byte[] ValidWebpVp8XBytes =
        Convert.FromBase64String("UklGRtwAAABXRUJQVlA4WAoAAAAIAAAAAAAAAAAAVlA4TAkAAAAvAAAAAIiI/gcARVhJRqwAAABJSSoACAAAAAYAEgEDAAEAAAABAAAAGgEFAAEAAABWAAAAGwEFAAEAAABeAAAAKAEDAAEAAAACAAAAMQECABAAAABmAAAAaYcEAAEAAAB2AAAAAAAAAGAAAAABAAAAYAAAAAEAAABwYWludC5uZXQgNC4yLjUABAAAkAcABAAAADAyMzABoAMAAQAAAAEAAAACoAQAAQAAAAEAAAADoAQAAQAAAAEAAAAAAAAA");

    // WebP lossless format (VP8L)
    private static readonly byte[] ValidWebpVp8LBytes =
        Convert.FromBase64String("UklGRhoAAABXRUJQVlA4TAUAAAAvAAAAAAA=");

    // WebP lossy format (VP8 )
    private static readonly byte[] ValidWebpVp8Bytes =
        Convert.FromBase64String("UklGRh4AAABXRUJQVlA4IAoAAAAAAACdASoBAAEA");

    [Fact]
    public void GetImageInfo_ValidPng_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.png", "image/png", ValidPngBytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_ValidJpeg_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.jpg", "image/jpeg", ValidJpegBytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_ValidWebp_Extended_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.webp", "image/webp", ValidWebpVp8XBytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_ValidWebp_Lossless_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.webp", "image/webp", ValidWebpVp8LBytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_ValidWebp_Lossy_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.webp", "image/webp", ValidWebpVp8Bytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_EmptyBytes_ReturnsNull()
    {
        FileImageInfo? result = _service.GetImageInfo("empty.png", "image/png", []);

        Assert.Null(result);
    }

    [Fact]
    public void GetImageInfo_SvgContentType_ReturnsNull()
    {
        FileImageInfo? result = _service.GetImageInfo("test.svg", "image/svg+xml", ValidPngBytes);

        Assert.Null(result);
    }

    [Fact]
    public void GetImageInfo_NonImageContentType_ReturnsNull()
    {
        FileImageInfo? result = _service.GetImageInfo("test.pdf", "application/pdf", ValidPngBytes);

        Assert.Null(result);
    }

    [Fact]
    public void GetImageInfo_CorruptBytes_ReturnsNull()
    {
        byte[] corruptBytes = [0x01, 0x02, 0x03, 0x04];
        FileImageInfo? result = _service.GetImageInfo("corrupt.png", "image/png", corruptBytes);

        Assert.Null(result);
    }
}
