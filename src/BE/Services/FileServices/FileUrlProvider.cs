using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace Chats.BE.Services.FileServices;

public class FileUrlProvider(ChatsDB db, FileServiceFactory fileServiceFactory, IUrlEncryptionService urlEncryptionService)
{
    public async Task<StepContent> CreateOpenAIImagePart(DB.File file, CancellationToken cancellationToken)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        if (file.FileService.FileServiceTypeId == (byte)DBFileServiceType.Local)
        {
            return await CreateOpenAIImagePartForceDownload(file, cancellationToken);
        }
        else
        {
            string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
            return StepContent.FromFileUrl(url);
        }
    }

    public async Task<StepContent> CreateOpenAIImagePartForceDownload(DB.File file, CancellationToken cancellationToken)
    {
        MemoryStream ms = new();
        IFileService fs = fileServiceFactory.Create(file.FileService);
        using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return StepContent.FromFileBlob(binaryData.ToArray(), file.MediaType);
    }

    public StepContent CreateOpenAITextUrl(DB.File file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
        return StepContent.FromFileUrl(url);
    }

    public FileDto CreateFileDto(DB.File file, bool tryWithUrl = true)
    {
        string? downloadUrl = null;
        if (tryWithUrl && file.FileService != null)
        {
            IFileService fs = fileServiceFactory.Create(file.FileService);
            downloadUrl = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file)).ToString();
        }

        return new FileDto()
        {
            Id = urlEncryptionService.EncryptFileId(file.Id),
            FileName = file.FileName,
            ContentType = file.MediaType,
            Url = downloadUrl,
        };
    }

    public async Task<StepContent> CreateFileContent(string encryptedFileId, CancellationToken cancellationToken)
    {
        int fileId = urlEncryptionService.DecryptFileId(encryptedFileId);
        DB.File file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.FileImageInfo)
            .FirstAsync(x => x.Id == fileId, cancellationToken);
        return StepContent.FromFile(file);
    }
}
