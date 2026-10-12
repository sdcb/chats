using System.Buffers.Binary;
using Chats.DB;
using SharpAstro.Codecs.Abstractions;
using SharpAstro.Jpeg;
using SharpAstro.Png;

namespace Chats.BE.Services.FileServices;

public class FileImageInfoService(ILogger<FileImageInfoService> logger)
{
    /// <summary>
    /// Get image info from a byte array.
    /// </summary>
    public FileImageInfo? GetImageInfo(string fileName, string contentType, byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
        {
            return null;
        }

        if (contentType == "image/svg+xml")
        {
            return null;
        }

        // Only process image files
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            ReadOnlySpan<byte> span = imageBytes;

            if (PngImageDecoder.CanDecode(span))
            {
                if (PngImageDecoder.TryReadInfo(span, out ImageInfo pngInfo))
                {
                    return new FileImageInfo
                    {
                        Width = pngInfo.Width,
                        Height = pngInfo.Height
                    };
                }
            }
            else if (JpegImageDecoder.CanDecode(span))
            {
                if (JpegImageDecoder.TryReadInfo(span, out ImageInfo jpegInfo))
                {
                    return new FileImageInfo
                    {
                        Width = jpegInfo.Width,
                        Height = jpegInfo.Height
                    };
                }
            }
            else if (TryReadWebpInfo(span, out int webpWidth, out int webpHeight))
            {
                return new FileImageInfo
                {
                    Width = webpWidth,
                    Height = webpHeight
                };
            }

            logger.LogWarning("Failed to identify image for {fileName}({contentType})", fileName, contentType);
            return null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get image size for {fileName}({contentType})", fileName, contentType);
            return null;
        }
    }

    private static bool TryReadWebpInfo(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 25)
        {
            return false;
        }

        if (!data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return false;
        }

        int offset = 12;
        while (offset + 8 <= data.Length)
        {
            ReadOnlySpan<byte> chunkType = data.Slice(offset, 4);
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
            offset += 8;

            if (chunkSize < 0 || offset + chunkSize > data.Length)
            {
                return false;
            }

            if (chunkType.SequenceEqual("VP8 "u8))
            {
                if (offset + 10 > data.Length) return false;
                if (data[offset + 3] != 0x9D || data[offset + 4] != 0x01 || data[offset + 5] != 0x2A)
                {
                    return false;
                }
                width = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 6, 2)) & 0x3FFF;
                height = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 8, 2)) & 0x3FFF;
                return width > 0 && height > 0;
            }
            else if (chunkType.SequenceEqual("VP8L"u8))
            {
                if (offset + 5 > data.Length) return false;
                if (data[offset] != 0x2F) return false;
                uint bits = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 1, 4));
                width = (int)((bits & 0x3FFF) + 1);
                height = (int)(((bits >> 14) & 0x3FFF) + 1);
                return width > 0 && height > 0;
            }
            else if (chunkType.SequenceEqual("VP8X"u8))
            {
                if (offset + 10 > data.Length) return false;
                width = (data[offset + 4] | (data[offset + 5] << 8) | (data[offset + 6] << 16)) + 1;
                height = (data[offset + 7] | (data[offset + 8] << 8) | (data[offset + 9] << 16)) + 1;
                return width > 0 && height > 0;
            }

            offset += chunkSize + (chunkSize & 1);
        }

        return false;
    }
}
