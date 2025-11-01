using Chats.BE.DB.Enums;

namespace Chats.BE.DB;

/// <summary>
/// 静态的文件服务类型信息，替代数据库中的 FileServiceType 表
/// </summary>
public static class FileServiceTypeInfo
{
    private record ServiceTypeInfo(
        DBFileServiceType Id,
        string Name,
        string InitialConfig
    );

    private static readonly Dictionary<DBFileServiceType, ServiceTypeInfo> _serviceTypes = new()
    {
        [DBFileServiceType.Local] = new(
            DBFileServiceType.Local,
            "Local",
            "./AppData/Files"
        ),
        [DBFileServiceType.Minio] = new(
            DBFileServiceType.Minio,
            "Minio",
            """{"endpoint": "https://minio.example.com", "accessKey": "your-access-key", "secretKey": "your-secret-key", "bucket": "your-bucket", "region": null}"""
        ),
        [DBFileServiceType.AwsS3] = new(
            DBFileServiceType.AwsS3,
            "AWS S3",
            """{"region": "ap-southeast-1", "accessKeyId": "your-access-key-id", "secretAccessKey": "your-secret-access-key", "bucket": "your-bucket"}"""
        ),
        [DBFileServiceType.AliyunOSS] = new(
            DBFileServiceType.AliyunOSS,
            "Aliyun OSS",
            """{"endpoint": "oss-cn-hangzhou.aliyuncs.com", "accessKeyId": "your-access-key-id", "accessKeySecret": "your-access-key-secret", "bucket": "your-bucket"}"""
        ),
        [DBFileServiceType.AzureBlobStorage] = new(
            DBFileServiceType.AzureBlobStorage,
            "Azure Blob Storage",
            """{"connectionString": "DefaultEndpointsProtocol=https;AccountName=your-account-name;AccountKey=your-account-key;EndpointSuffix=core.windows.net", "containerName": "YourContainerName"}"""
        ),
    };

    public static string GetName(DBFileServiceType serviceTypeId)
    {
        return _serviceTypes.TryGetValue(serviceTypeId, out var info) ? info.Name : serviceTypeId.ToString();
    }

    public static string GetInitialConfig(DBFileServiceType serviceTypeId)
    {
        return _serviceTypes.TryGetValue(serviceTypeId, out var info) ? info.InitialConfig : string.Empty;
    }

    public static IEnumerable<DBFileServiceType> GetAllServiceTypeIds()
    {
        return _serviceTypes.Keys;
    }

    public static bool IsValidServiceTypeId(DBFileServiceType serviceTypeId)
    {
        return _serviceTypes.ContainsKey(serviceTypeId);
    }
}
