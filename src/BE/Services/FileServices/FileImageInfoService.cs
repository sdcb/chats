using Chats.BE.DB;
using Chats.BE.Services.ImageInfo;
using System.Drawing;

namespace Chats.BE.Services.FileServices;

public class FileImageInfoService(ILogger<FileImageInfoService> logger)
{
    public FileImageInfo? GetImageInfo(string fileName, string contentType, byte[] imageFirst4KBytes)
    {
        try
        {
            IImageInfoService iis = ImageInfoFactory.CreateImageInfoService(contentType);
            Size size = iis.GetImageSize(imageFirst4KBytes);
            return new FileImageInfo
            {
                Width = size.Width,
                Height = size.Height
            };
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get image size for {fileName}({contentType})", fileName, contentType);
            return null;
        }
    }
}
