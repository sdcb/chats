using Chats.Web.Services.FileServices.Implementations.AwsS3;

namespace Chats.Web.Services.FileServices.Implementations.Minio;

public class MinioFileService(MinioConfig config) : AwsS3FileService(config.Bucket, config.CreateS3());
