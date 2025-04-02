using Chats.BE.DB;
using Chats.BE.Infrastructure;
using File = Chats.BE.DB.File;

namespace Chats.BE.Services.FileServices;

public class DBFileService(ChatsDB db, FileServiceFactory fsf, FileContentTypeService fstService, ClientInfoManager clientInfoManager, CurrentUser currentUser, FileImageInfoService fiis)
{
    public async Task<File> StoreNoSave(DBFileDef def, IFileService? fs = null, ClientInfo? knowClientInfo = null, CancellationToken cancellationToken = default)
    {
        fs ??= await CreateDefault(cancellationToken);

        string storageKey = await fs.Upload(new FileUploadRequest
        {
            Stream = new MemoryStream(def.Bytes),
            FileName = def.FileName,
            ContentType = def.ContentType
        }, cancellationToken);

        FileContentType fileContentType = await fstService.GetOrCreate(def.ContentType, cancellationToken);
        ClientInfo clientInfo = knowClientInfo ?? await clientInfoManager.GetClientInfo(cancellationToken);
        File file = new()
        {
            FileName = def.FileName,
            FileContentType = fileContentType,
            FileContentTypeId = fileContentType.Id,
            StorageKey = storageKey,
            Size = def.Bytes.Length,
            ClientInfo = clientInfo,
            ClientInfoId = clientInfo.Id,
            CreatedAt = DateTime.UtcNow,
            CreateUserId = currentUser.Id,
            FileServiceId = fs.Id,
            FileImageInfo = fiis.GetImageInfo(def.FileName, def.ContentType, def.Bytes)
        };

        return file;
    }

    public async Task<IFileService> CreateDefault(CancellationToken cancellationToken)
    {
        FileService? dbfs = await FileService.GetDefault(db, cancellationToken) ?? throw new InvalidOperationException("Default file service config not found.");
        return fsf.Create(dbfs);
    }
}
