using Aliyun.OSS;
using Chats.BE.DB.Enums;
using System.Net;

namespace Chats.BE.Services.FileServices.Implementations.AliyunOSS;

public class AliyunOSSFileService(int id, DBFileServiceType fileServiceType, AliyunOssConfig config) : IFileService(id, fileServiceType)
{
    private readonly OssClient _oss = new(config.Endpoint, config.AccessKeyId, config.AccessKeySecret);

    public override Uri CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        return _oss.GeneratePresignedUri(config.Bucket, req.StorageKey, req.ValidEnd.UtcDateTime, SignHttpMethod.Get);
    }

    public override Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OssObject obj = _oss.GetObject(config.Bucket, storageKey);
        return Task.FromResult(obj.ResponseStream);
    }

    public override Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SuggestedStorageInfo ssi = SuggestedStorageInfo.FromFileName(request.FileName);
        _ = _oss.PutObject(config.Bucket, ssi.StorageKey, request.Stream);
        return Task.FromResult(ssi.StorageKey);
    }

    public override Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteObjectResult r = _oss.DeleteObject(config.Bucket, storageKey);
        return Task.FromResult(r.HttpStatusCode == HttpStatusCode.NoContent);
    }
}
