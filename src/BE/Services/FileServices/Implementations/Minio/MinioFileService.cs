using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices.Implementations.AwsS3;

namespace Chats.BE.Services.FileServices.Implementations.Minio;

public class MinioFileService(int id, DBFileServiceType fileServiceType, MinioConfig config) : AwsS3FileService(id, fileServiceType, config.Bucket, config.CreateS3());
