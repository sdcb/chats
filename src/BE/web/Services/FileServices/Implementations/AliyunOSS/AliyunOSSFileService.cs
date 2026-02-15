using Aliyun.OSS;
using System.Net;

namespace Chats.BE.Services.FileServices.Implementations.AliyunOSS;

public class AliyunOSSFileService(AliyunOssConfig config) : IFileService
{
    private readonly OssClient _oss = new(config.Endpoint, config.AccessKeyId, config.AccessKeySecret);

    public string CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        GeneratePresignedUriRequest request = new(config.Bucket, req.StorageKey, SignHttpMethod.Get)
        {
            Expiration = req.ValidEnd.UtcDateTime,
            ResponseHeaders = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{req.FileName}\""
            }
        };
        return _oss.GeneratePresignedUri(request).ToString();
    }

    public Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OssObject obj = _oss.GetObject(config.Bucket, storageKey);
        return Task.FromResult(obj.ResponseStream);
    }

    public Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SuggestedStorageInfo ssi = SuggestedStorageInfo.FromFileName(request.FileName);
        _ = _oss.PutObject(config.Bucket, ssi.StorageKey, request.Stream);
        return Task.FromResult(ssi.StorageKey);
    }

    public Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteObjectResult r = _oss.DeleteObject(config.Bucket, storageKey);
        return Task.FromResult(r.HttpStatusCode == HttpStatusCode.NoContent);
    }
}
