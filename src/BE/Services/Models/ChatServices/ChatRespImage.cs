using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services.FileServices;

namespace Chats.BE.Services.Models.ChatServices;

public abstract record ChatRespImage
{
    public async Task<MessageContent> ToDB(
        IFileService fileService, 
        FileContentTypeService contentTypeService, 
        ClientInfoManager clientInfoManager,
        CurrentUser currentUser,
        int fileServiceId,
        FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken = default)
    {
        DBFileDef def = await Download(cancellationToken);
        string storageKey = await fileService.Upload(new FileUploadRequest
        {
            Stream = new MemoryStream(def.Bytes),
            FileName = def.FileName,
            ContentType = def.ContentType
        }, cancellationToken);

        return new MessageContent()
        {
            ContentTypeId = (byte)DBMessageContentType.FileId,
            MessageContentFile = new()
            {
                File = new()
                {
                    FileName = def.FileName,
                    FileContentType = await contentTypeService.GetOrCreate(def.ContentType, cancellationToken),
                    StorageKey = storageKey,
                    Size = def.Bytes.Length,
                    ClientInfo = await clientInfoManager.GetClientInfo(cancellationToken),
                    CreatedAt = DateTime.UtcNow,
                    CreateUserId = currentUser.Id,
                    FileServiceId = fileServiceId,
                    FileImageInfo = fileImageInfoService.GetImageInfo(def.FileName, def.ContentType, def.Bytes)
                }
            }
        };
    }

    public abstract Task<DBFileDef> Download(CancellationToken cancellationToken = default);
}

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

public record Base64Image : ChatRespImage
{
    public required string ContentType { get; init; }

    public required string Base64 { get; init; }

    public override Task<DBFileDef> Download(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = Convert.FromBase64String(Base64);
        return Task.FromResult(new DBFileDef(bytes, ContentType, null));
    }
}

public record UrlImage : ChatRespImage
{
    public required string Url { get; init; }

    public override async Task<DBFileDef> Download(CancellationToken cancellationToken = default)
    {
        using HttpClient client = new();
        HttpResponseMessage resp = await client.GetAsync(Url, cancellationToken);
        resp.EnsureSuccessStatusCode();

        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
        string contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string? fileName = resp.Content.Headers.ContentDisposition?.FileName;
        return new DBFileDef(bytes, contentType, fileName);
    }
}