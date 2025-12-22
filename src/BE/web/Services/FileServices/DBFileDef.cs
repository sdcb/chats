namespace Chats.Web.Services.FileServices;

public record DBFileDef(byte[] Bytes, string ContentType, string? SuggestedFileName)
{
    public string FileName => SuggestedFileName ?? MakeFileNameByContentType(ContentType);

    internal static string MakeFileNameByContentType(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => "image.jpg",
            "image/png" => "image.png",
            "image/gif" => "image.gif",
            "image/webp" => "image.webp",
            "image/svg+xml" => "image.svg",
            _ => "image"
        };
    }
}
