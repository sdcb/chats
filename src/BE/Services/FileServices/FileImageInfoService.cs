using Chats.BE.DB;
using SixLabors.ImageSharp;

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
            ImageInfo image = Image.Identify(imageBytes);
            if (image == null)
            {
                logger.LogWarning("Failed to identify image for {fileName}({contentType})", fileName, contentType);
                return null;
            }

            return new FileImageInfo
            {
                Width = image.Width,
                Height = image.Height
            };
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get image size for {fileName}({contentType})", fileName, contentType);
            return null;
        }
    }
}
