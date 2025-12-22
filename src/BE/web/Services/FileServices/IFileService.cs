namespace Chats.Web.Services.FileServices;

/// <summary>
/// Abstract for file service operations such as upload, download, and creating download URLs.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Uploads a file to the storage. The caller must dispose the stream.
    /// </summary>
    /// <param name="request">The file upload request containing file details.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the storage key of the uploaded file.</returns>
    /// <remarks>
    /// The <see cref="Stream"/> inside <see cref="FileUploadRequest"/> will not be disposed by the file service.
    /// </remarks>
    public abstract Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the storage.
    /// </summary>
    /// <param name="storageKey">The storage key of the file to download.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the file stream.
    /// The caller must dispose the stream.
    /// </returns>
    public abstract Task<Stream> Download(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a download URL for a file.
    /// </summary>
    /// <param name="request">The request containing details for creating the download URL.</param>
    /// <returns>The URI of the created download URL.</returns>
    public abstract string CreateDownloadUrl(CreateDownloadUrlRequest request);

    /// <summary>
    /// Deletes a file from the storage.
    /// </summary>
    /// <param name="storageKey">The storage key of the file to delete.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the file was deleted.</returns>
    public abstract Task<bool> Delete(string storageKey, CancellationToken cancellationToken = default);
}
