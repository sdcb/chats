namespace Chats.Web.DB.Enums;

public enum DBFileServiceType : byte
{
    Local = 0,
    Minio = 1,
    AwsS3 = 2,
    AliyunOSS = 3,
    AzureBlobStorage = 4
}
