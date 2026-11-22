using Chats.DB;
using Sdcb.SimdCodecs;
using Sdcb.SimdCodecs.Abstractions;

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
            if (ImageCodecs.TryReadInfo(imageBytes, out ImageInfo info))
            {
                return new FileImageInfo
                {
                    Width = info.Width,
                    Height = info.Height
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
}
