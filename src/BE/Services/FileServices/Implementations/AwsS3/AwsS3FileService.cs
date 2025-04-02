using Amazon.S3;
using Amazon.S3.Model;
using Chats.BE.DB.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Chats.BE.Services.FileServices.Implementations.AwsS3;

public class AwsS3FileService(int id, DBFileServiceType fileServiceType) : IFileService(id, fileServiceType)
{
    private readonly string _bucketName = null!;
    private readonly AmazonS3Client _s3 = null!;

    public AwsS3FileService(int id, DBFileServiceType fileServiceType, AwsS3Config config) : this(id, fileServiceType) 
    {
        _bucketName = config.Bucket;
        _s3 = config.CreateS3();
    }

    public AwsS3FileService(int id, DBFileServiceType fileServiceType, string bucketName, AmazonS3Client s3) : this(id, fileServiceType)
    {
        _bucketName = bucketName;
        _s3 = s3;
    }

    public override Uri CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        string url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = req.StorageKey,
            Expires = req.ValidEnd.UtcDateTime,
            Verb = HttpVerb.GET
        });
        return new Uri(url);
    }

    public override async Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        GetObjectResponse resp = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        }, cancellationToken);
        return resp.ResponseStream;
    }

    public override async Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
    {
        SuggestedStorageInfo ssi = SuggestedStorageInfo.FromFileName(request.FileName);
        _ = await _s3.PutObjectAsync(new PutObjectRequest()
        {
            BucketName = _bucketName,
            Key = ssi.StorageKey,
            InputStream = request.Stream,
            ContentType = request.ContentType
        }, cancellationToken);
        return ssi.StorageKey;
    }

    public override async Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        DeleteObjectResponse resp = await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        }, cancellationToken);
        return resp.HttpStatusCode == HttpStatusCode.NoContent; // to be confirmed
    }
}
