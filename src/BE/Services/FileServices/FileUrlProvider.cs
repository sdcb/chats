﻿using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace Chats.BE.Services.FileServices;

public class FileUrlProvider(ChatsDB db, FileServiceFactory fileServiceFactory, IUrlEncryptionService urlEncryptionService)
{
    public async Task<ChatMessageContentPart> CreateOpenAIPart(MessageContentFile? mcFile, CancellationToken cancellationToken)
    {
        if (mcFile == null)
        {
            if (mcFile?.File?.FileService == null || mcFile.File.FileContentType == null)
            {
                throw new ArgumentNullException(nameof(mcFile));
            }
            throw new ArgumentNullException(nameof(mcFile));
        }

        DB.File file = mcFile.File;

        DBFileServiceType fileServiceType = (DBFileServiceType)file.FileService.FileServiceTypeId;
        IFileService fs = fileServiceFactory.Create(fileServiceType, file.FileService.Configs);
        if (file.FileService.FileServiceTypeId == (byte)DBFileServiceType.Local)
        {
            MemoryStream ms = new();
            using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            BinaryData binaryData = BinaryData.FromStream(ms);
            return ChatMessageContentPart.CreateImagePart(binaryData, file.FileContentType.ContentType);
        }
        else
        {
            Uri url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
            return ChatMessageContentPart.CreateImagePart(url);
        }
    }

    public FileDto CreateFileDto(DB.File file)
    {
        DBFileServiceType fileServiceType = (DBFileServiceType)file.FileService.FileServiceTypeId;
        IFileService fs = fileServiceFactory.Create(fileServiceType, file.FileService.Configs);
        Uri downloadUrl = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));

        return new FileDto
        {
            Id = urlEncryptionService.EncryptFileId(file.Id),
            Url = downloadUrl,
        };
    }

    public async Task<MessageContent> CreateFileContent(string encryptedFileId, CancellationToken cancellationToken)
    {
        int fileId = urlEncryptionService.DecryptFileId(encryptedFileId);
        DB.File file = await db.Files
            .Include(x => x.FileContentType)
            .Include(x => x.FileService)
            .Include(x => x.FileImageInfo)
            .FirstAsync(x => x.Id == fileId, cancellationToken);
        return MessageContent.FromFile(fileId, file);
    }
}
