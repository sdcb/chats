﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Chats.BE.DB.Enums;

namespace Chats.BE.Services.FileServices.Implementations.AzureBlobStorage;

public class AzureBlobStorageFileService(int id, DBFileServiceType fileServiceType, AzureBlobStorageConfig config) : IFileService(id, fileServiceType)
{
    private readonly BlobContainerClient _containerClient = new(config.ConnectionString, config.ContainerName);

    public override Uri CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(req.StorageKey);
        return blobClient.GenerateSasUri(BlobSasPermissions.Read, req.ValidEnd);
    }

    public override async Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(storageKey);
        return await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public override Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(storageKey);
        return blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
    }

    public override async Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
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
