using Chats.BE.DB.Enums;
using Chats.BE.Services.UrlEncryption;

namespace Chats.BE.Services.FileServices.Implementations.Local;

public class LocalFileService(int id, DBFileServiceType fileServiceType, string localFolder, HostUrlService hostUrlservice, IUrlEncryptionService urlEncryption) : IFileService(id, fileServiceType)
{
    public override Uri CreateDownloadUrl(CreateDownloadUrlRequest request)
    {
        TimedId timedId = TimedId.CreateFor(request.FileId, request.ValidPeriod);
        string path = urlEncryption.CreateFileIdPath(timedId);
        return new Uri($"{hostUrlservice.GetBEUrl()}/api/file/{path}");
    }

    public override Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string localPath = Path.Combine(localFolder, storageKey);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            return Task.FromResult(true);
        }
        else
        {
            return Task.FromResult(false);
        }
    }

    public override Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string localPath = Path.Combine(localFolder, storageKey);
        return Task.FromResult<Stream>(File.OpenRead(localPath));
    }

    public override async Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SuggestedStorageInfo suggestedStorageInfo = SuggestedStorageInfo.FromFileName(request.FileName);
        string folderPath = Path.Combine(localFolder, suggestedStorageInfo.Folder);
        Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, suggestedStorageInfo.FileName);
        using FileStream fileStream = File.Create(filePath);
        await request.Stream.CopyToAsync(fileStream, cancellationToken);

        return suggestedStorageInfo.StorageKey;
    }
}
