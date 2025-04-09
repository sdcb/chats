using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices.Implementations.AliyunOSS;
using Chats.BE.Services.FileServices.Implementations.AwsS3;
using Chats.BE.Services.FileServices.Implementations.AzureBlobStorage;
using Chats.BE.Services.FileServices.Implementations.Local;
using Chats.BE.Services.FileServices.Implementations.Minio;
using Chats.BE.Services.UrlEncryption;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Chats.BE.Services.FileServices;

public class FileServiceFactory(HostUrlService hostUrlService, IUrlEncryptionService urlEncryption)
{
    private readonly ConcurrentDictionary<CacheKey, IFileService> _cache = [];

    public IFileService Create(FileService dbfs)
    {
        ArgumentNullException.ThrowIfNull(dbfs);
        CacheKey key = new((DBFileServiceType)dbfs.FileServiceTypeId, dbfs.Configs);
        if (_cache.TryGetValue(key, out IFileService? fileService))
        {
            return fileService;
        }
        fileService = CreateNoCache(dbfs);
        _cache[key] = fileService;
        return fileService;
    }

    private IFileService CreateNoCache(FileService dbfs)
    {
        DBFileServiceType fst = (DBFileServiceType)dbfs.FileServiceTypeId;
        return fst switch
        {
            DBFileServiceType.Local => new LocalFileService(dbfs.Configs, hostUrlService, urlEncryption),
            DBFileServiceType.Minio => new MinioFileService(JsonSerializer.Deserialize<MinioConfig>(dbfs.Configs)!),
            DBFileServiceType.AwsS3 => new AwsS3FileService(JsonSerializer.Deserialize<AwsS3Config>(dbfs.Configs)!),
            DBFileServiceType.AliyunOSS => new AliyunOSSFileService(JsonSerializer.Deserialize<AliyunOssConfig>(dbfs.Configs)!),
            DBFileServiceType.AzureBlobStorage => new AzureBlobStorageFileService(JsonSerializer.Deserialize<AzureBlobStorageConfig>(dbfs.Configs)!),
            _ => throw new ArgumentException($"Unsupported file service type: {dbfs.FileServiceTypeId}")
        };
    }

    private record CacheKey(DBFileServiceType FileServiceType, string Config);
}
