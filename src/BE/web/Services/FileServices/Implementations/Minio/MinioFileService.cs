using Chats.BE.Services.FileServices.Implementations.AwsS3;

namespace Chats.BE.Services.FileServices.Implementations.Minio;

public class MinioFileService(MinioConfig config) : AwsS3FileService(config.Bucket, config.CreateS3());
