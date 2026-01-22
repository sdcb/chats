using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using DBFile = Chats.DB.File;

namespace Chats.BE.Services.FileServices;

public class FileUrlProvider(ChatsDB db, IFileServiceFactory fileServiceFactory, IUrlEncryptionService urlEncryptionService)
{
    public async Task<StepContent> CreateOpenAIImagePart(DBFile file, CancellationToken cancellationToken)
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

    public async Task<StepContent> CreateOpenAIImagePartForceDownload(DBFile file, CancellationToken cancellationToken)
    {
        MemoryStream ms = new();
        IFileService fs = fileServiceFactory.Create(file.FileService);
        using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return StepContent.FromFileBlob(binaryData.ToArray(), file.MediaType);
    }

    public StepContent CreateOpenAITextUrl(DBFile file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
        return StepContent.FromFileUrl(url);
    }

    public FileDto CreateFileDto(DBFile file, bool tryWithUrl = true)
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
        DBFile file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.FileImageInfo)
            .FirstAsync(x => x.Id == fileId, cancellationToken);
        return StepContent.FromFile(file);
    }

    #region Neutral Content Methods

    public NeutralContent CreateNeutralImagePart(DBFile file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        if (file.FileService.FileServiceTypeId == (byte)DBFileServiceType.Local)
        {
            return CreateNeutralImagePartForceDownloadInternal(file, fs);
        }
        else
        {
            string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
            return NeutralFileUrlContent.Create(url);
        }
    }

    public async Task<NeutralContent> CreateNeutralImagePartAsync(DBFile file, CancellationToken cancellationToken)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        if (file.FileService.FileServiceTypeId == (byte)DBFileServiceType.Local)
        {
            return await CreateNeutralImagePartForceDownloadInternalAsync(file, fs, cancellationToken);
        }
        else
        {
            string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
            return NeutralFileUrlContent.Create(url);
        }
    }

    public NeutralContent CreateNeutralImagePartForceDownload(DBFile file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        return CreateNeutralImagePartForceDownloadInternal(file, fs);
    }

    public async Task<NeutralContent> CreateNeutralImagePartForceDownloadAsync(DBFile file, CancellationToken cancellationToken)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        return await CreateNeutralImagePartForceDownloadInternalAsync(file, fs, cancellationToken);
    }

    private static NeutralContent CreateNeutralImagePartForceDownloadInternal(DBFile file, IFileService fs)
    {
        using Stream stream = fs.Download(file.StorageKey, CancellationToken.None).Result;
        MemoryStream ms = new();
        stream.CopyTo(ms);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return NeutralFileBlobContent.Create(binaryData.ToArray(), file.MediaType);
    }

    private static async Task<NeutralContent> CreateNeutralImagePartForceDownloadInternalAsync(DBFile file, IFileService fs, CancellationToken cancellationToken)
    {
        MemoryStream ms = new();
        using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return NeutralFileBlobContent.Create(binaryData.ToArray(), file.MediaType);
    }

    public NeutralContent CreateNeutralTextUrl(DBFile file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
        return NeutralTextContent.Create(url);
    }

    #endregion
}
