using Chats.BE.DB;
using SkiaSharp;

namespace Chats.BE.Services.FileServices;

public class FileImageInfoService(ILogger<FileImageInfoService> logger)
{
    /// <summary>
    /// Get image info from a byte array.
    /// </summary>
    public FileImageInfo? GetImageInfo(string fileName, string contentType, byte[] imageBytes)
    {
        // Only process image files
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new MemoryStream(imageBytes);
            using SKCodec codec = SKCodec.Create(stream);
            if (codec == null)
            {
                logger.LogWarning("Failed to create codec for {fileName}({contentType})", fileName, contentType);
                return null;
            }

            return new FileImageInfo
            {
                Width = codec.Info.Width,
                Height = codec.Info.Height
            };
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get image size for {fileName}({contentType})", fileName, contentType);
            return null;
        }
    }
}
