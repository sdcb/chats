using Chats.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models.ChatServices;
using DBFile = Chats.DB.File;

namespace Chats.BE.Services.FileServices;

public class DBFileService(ChatsDB db, IFileServiceFactory fsf, CurrentUser currentUser, FileImageInfoService fiis)
{
    public async Task<DBFile> StoreImage(ImageChatSegment image, int clientInfoId, FileService dbfs, CancellationToken cancellationToken = default)
    {
        DBFileDef def = await image.Download(cancellationToken);
        IFileService fs = fsf.Create(dbfs);

        // Get image info before upload
        FileImageInfo? imageInfo = fiis.GetImageInfo(def.FileName, def.ContentType, def.Bytes);

        string storageKey = await fs.Upload(new FileUploadRequest
        {
            Stream = new MemoryStream(def.Bytes),
            FileName = def.FileName,
            ContentType = def.ContentType
        }, cancellationToken);

        DBFile file = new()
        {
            FileName = def.FileName,
            MediaType = def.ContentType,
            StorageKey = storageKey,
            Size = def.Bytes.Length,
            ClientInfoId = clientInfoId,
            CreatedAt = DateTime.UtcNow,
            CreateUserId = currentUser.Id,
            FileServiceId = dbfs.Id,
            FileService = dbfs,
            FileImageInfo = imageInfo,
        };
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        return file;
    }

    public async Task<DBFile> StoreFileBytes(byte[] bytes, string fileName, string contentType, int clientInfoId, FileService dbfs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName is required", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";

        IFileService fs = fsf.Create(dbfs);

        FileImageInfo? imageInfo = fiis.GetImageInfo(fileName, contentType, bytes);

        string storageKey;
        using (MemoryStream uploadStream = new(bytes))
        {
            storageKey = await fs.Upload(new FileUploadRequest
            {
                Stream = uploadStream,
                FileName = fileName,
                ContentType = contentType,
            }, cancellationToken);
        }

        DBFile file = new()
        {
            FileName = fileName,
            MediaType = contentType,
            StorageKey = storageKey,
            Size = bytes.Length,
            ClientInfoId = clientInfoId,
            CreatedAt = DateTime.UtcNow,
            CreateUserId = currentUser.Id,
            FileServiceId = dbfs.Id,
            FileService = dbfs,
            FileImageInfo = imageInfo,
        };
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        return file;
    }
}
