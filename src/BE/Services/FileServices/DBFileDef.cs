namespace Chats.BE.Services.FileServices;

public record DBFileDef(byte[] Bytes, string ContentType, string? SuggestedFileName)
{
    public string FileName => SuggestedFileName ?? MakeFileNameByContentType(ContentType);

    protected static string MakeFileNameByContentType(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => "image.jpg",
            "image/png" => "image.png",
            "image/gif" => "image.gif",
            _ => "image"
        };
    }
}
