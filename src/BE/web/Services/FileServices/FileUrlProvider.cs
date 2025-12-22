using Chats.Web.Controllers.Chats.Messages.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services.Models.Neutral;
using Chats.Web.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Services.FileServices;

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

    #region Neutral Content Methods

    public NeutralContent CreateNeutralImagePart(DB.File file)
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

    public async Task<NeutralContent> CreateNeutralImagePartAsync(DB.File file, CancellationToken cancellationToken)
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

    public NeutralContent CreateNeutralImagePartForceDownload(DB.File file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        return CreateNeutralImagePartForceDownloadInternal(file, fs);
    }

    public async Task<NeutralContent> CreateNeutralImagePartForceDownloadAsync(DB.File file, CancellationToken cancellationToken)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        return await CreateNeutralImagePartForceDownloadInternalAsync(file, fs, cancellationToken);
    }

    private static NeutralContent CreateNeutralImagePartForceDownloadInternal(DB.File file, IFileService fs)
    {
        using Stream stream = fs.Download(file.StorageKey, CancellationToken.None).Result;
        MemoryStream ms = new();
        stream.CopyTo(ms);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return NeutralFileBlobContent.Create(binaryData.ToArray(), file.MediaType);
    }

    private static async Task<NeutralContent> CreateNeutralImagePartForceDownloadInternalAsync(DB.File file, IFileService fs, CancellationToken cancellationToken)
    {
        MemoryStream ms = new();
        using Stream stream = await fs.Download(file.StorageKey, cancellationToken);
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        BinaryData binaryData = BinaryData.FromStream(ms);
        return NeutralFileBlobContent.Create(binaryData.ToArray(), file.MediaType);
    }

    public NeutralContent CreateNeutralTextUrl(DB.File file)
    {
        IFileService fs = fileServiceFactory.Create(file.FileService);
        string url = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
        return NeutralTextContent.Create(url);
    }

    #endregion
}
