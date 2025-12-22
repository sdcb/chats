using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Chats.Web.Services.FileServices.Implementations.AzureBlobStorage;

public class AzureBlobStorageFileService(AzureBlobStorageConfig config) : IFileService
{
    private readonly BlobContainerClient _containerClient = new(config.ConnectionString, config.ContainerName);

    public string CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(req.StorageKey);
        Uri url = blobClient.GenerateSasUri(BlobSasPermissions.Read, req.ValidEnd);
        return url.ToString();
    }

    public async Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(storageKey);
        return await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(storageKey);
        return blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
    }

    public async Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
    {
        SuggestedStorageInfo suggestedStorageInfo = SuggestedStorageInfo.FromFileName(request.FileName);
        BlobClient blobClient = _containerClient.GetBlobClient(suggestedStorageInfo.StorageKey);
        _ = await blobClient.UploadAsync(request.Stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = request.ContentType },
        }, cancellationToken);
        return suggestedStorageInfo.StorageKey;
    }
}
