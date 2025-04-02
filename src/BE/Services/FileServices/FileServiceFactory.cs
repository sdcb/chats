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
        CacheKey key = new(dbfs.Id, (DBFileServiceType)dbfs.FileServiceTypeId, dbfs.Configs);
        if (_cache.TryGetValue(key, out IFileService? fileService))
        {
            return fileService;
        }
        fileService = CreateNoCache(dbfs);
        _cache[key] = fileService;
        return fileService;
    }

    internal IFileService Create(DB.File file)
    {
        throw new NotImplementedException();
    }

    private IFileService CreateNoCache(FileService dbfs)
    {
        DBFileServiceType fst = (DBFileServiceType)dbfs.FileServiceTypeId;
        return fst switch
        {
            DBFileServiceType.Local => new LocalFileService(dbfs.Id, fst, dbfs.Configs, hostUrlService, urlEncryption),
            DBFileServiceType.Minio => new MinioFileService(dbfs.Id, fst, JsonSerializer.Deserialize<MinioConfig>(dbfs.Configs)!),
            DBFileServiceType.AwsS3 => new AwsS3FileService(dbfs.Id, fst, JsonSerializer.Deserialize<AwsS3Config>(dbfs.Configs)!),
            DBFileServiceType.AliyunOSS => new AliyunOSSFileService(dbfs.Id, fst, JsonSerializer.Deserialize<AliyunOssConfig>(dbfs.Configs)!),
            DBFileServiceType.AzureBlobStorage => new AzureBlobStorageFileService(dbfs.Id, fst, JsonSerializer.Deserialize<AzureBlobStorageConfig>(dbfs.Configs)!),
            _ => throw new ArgumentException($"Unsupported file service type: {dbfs.FileServiceTypeId}")
        };
    }

    private record CacheKey(int Id, DBFileServiceType FileServiceType, string Config);
}
