using Chats.Web.DB;
using Chats.Web.Infrastructure;
using Chats.Web.Services.Models.ChatServices;
using File = Chats.Web.DB.File;

namespace Chats.Web.Services.FileServices;

public class DBFileService(ChatsDB db, FileServiceFactory fsf, CurrentUser currentUser, FileImageInfoService fiis)
{
    public async Task<File> StoreImage(ImageChatSegment image, int clientInfoId, FileService dbfs, CancellationToken cancellationToken = default)
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

        File file = new()
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
}
