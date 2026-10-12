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

    private static readonly byte[] ValidBmpBytes =
        Convert.FromBase64String("Qk06AAAAAAAAADYAAAAoAAAAAQAAAAEAAAABABgAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAA////AA==");

    private static readonly byte[] ValidGifBytes =
        Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

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
    public void GetImageInfo_ValidBmp_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.bmp", "image/bmp", ValidBmpBytes);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void GetImageInfo_ValidGif_ReturnsDimensions()
    {
        FileImageInfo? result = _service.GetImageInfo("test.gif", "image/gif", ValidGifBytes);

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
