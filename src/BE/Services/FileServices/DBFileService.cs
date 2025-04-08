﻿using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models.ChatServices;
using File = Chats.BE.DB.File;

namespace Chats.BE.Services.FileServices;

public class DBFileService(ChatsDB db, FileServiceFactory fsf, ClientInfoManager clientInfoManager, CurrentUser currentUser, FileImageInfoService fiis)
{
    public async Task<File> StoreImage(ImageChatSegment image, ClientInfo clientInfo, FileService dbfs, CancellationToken cancellationToken = default)
    {
        DBFileDef def = await image.Download(cancellationToken);
        IFileService fs = fsf.Create(dbfs);
        FileContentTypeService fstService = new(db);

        string storageKey = await fs.Upload(new FileUploadRequest
        {
            Stream = new MemoryStream(def.Bytes),
            FileName = def.FileName,
            ContentType = def.ContentType
        }, cancellationToken);

        FileContentType fileContentType = await fstService.GetOrCreate(def.ContentType, cancellationToken);
        clientInfo ??= await clientInfoManager.GetClientInfo(cancellationToken);
        File file = new()
        {
            FileName = def.FileName,
            FileContentTypeId = fileContentType.Id,
            StorageKey = storageKey,
            Size = def.Bytes.Length,
            ClientInfo = clientInfo,
            ClientInfoId = clientInfo.Id,
            CreatedAt = DateTime.UtcNow,
            CreateUserId = currentUser.Id,
            FileServiceId = fs.Id,
            FileImageInfo = fiis.GetImageInfo(def.FileName, def.ContentType, def.Bytes),
        };
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        return file;
    }
}
