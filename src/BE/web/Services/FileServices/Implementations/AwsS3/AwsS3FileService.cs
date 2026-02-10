using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace Chats.BE.Services.FileServices.Implementations.AwsS3;

public class AwsS3FileService : IFileService
{
    private readonly string _bucketName = null!;
    private readonly AmazonS3Client _s3 = null!;

    public AwsS3FileService(AwsS3Config config)
    {
        _bucketName = config.Bucket;
        _s3 = config.CreateS3();
    }

    public AwsS3FileService(string bucketName, AmazonS3Client s3)
    {
        _bucketName = bucketName;
        _s3 = s3;
    }

    public string CreateDownloadUrl(CreateDownloadUrlRequest req)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _bucketName,
            Key = req.StorageKey,
            Expires = req.ValidEnd.UtcDateTime,
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"inline; filename=\"{req.FileName}\""
            }
        };

        string url = _s3.GetPreSignedURL(request);
        return url;
    }

    public async Task<Stream> Download(string storageKey, CancellationToken cancellationToken)
    {
        GetObjectResponse resp = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        }, cancellationToken);
        return resp.ResponseStream;
    }

    public async Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken)
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

    public async Task<bool> Delete(string storageKey, CancellationToken cancellationToken)
    {
        DeleteObjectResponse resp = await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        }, cancellationToken);
        return resp.HttpStatusCode == HttpStatusCode.NoContent; // to be confirmed
    }
}
