using AzureFileMigrator;
using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using DBFile = Chats.DB.File;

Console.WriteLine("Press enter to start the migration...");
Console.ReadLine();

// Load configuration from user secrets
IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

IHostEnvironment hostEnvironment = new HostingEnvironment
{
    EnvironmentName = "Development",
    ApplicationName = "AzureFileMigrator",
    ContentRootPath = Directory.GetCurrentDirectory()
};

using ChatsDB db = new(new DbContextOptionsBuilder<ChatsDB>()
    .UseSqlServer(configuration.GetConnectionString("ChatsDB"))
    .Options);
FileService[] interestedFileServices = db.FileServices
    .Where(x => x.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage || x.FileServiceTypeId == (byte)DBFileServiceType.Minio)
    .ToArray();
FileService azureConfig = interestedFileServices.Single(x => x.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage);
FileService minioConfig = interestedFileServices.Single(x => x.FileServiceTypeId == (byte)DBFileServiceType.Minio);
Console.WriteLine($"AzureBlobStorage: {azureConfig.Configs}");
Console.WriteLine($"Minio: {minioConfig.Configs}");

FileServiceFactory fileServiceFactory = new(new DummyHostUrlService(), new NoOpUrlEncryptionService());
IFileService azureFileService = fileServiceFactory.Create(azureConfig);
IFileService minioFileService = fileServiceFactory.Create(minioConfig);

List<DBFile> files = db.Files
    .Include(x => x.FileService)
    .Where(x => x.FileService.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage)
    .OrderByDescending(x => x.Id)
    .ToList();
for (int i = 0; i < files.Count; i++)
{
    DBFile file = files[i];
    Console.Write($"Processing {i + 1}/{files.Count}: {file.FileName}...");
    using Stream stream = await azureFileService.Download(file.StorageKey);
    string newStorageKey = await minioFileService.Upload(new FileUploadRequest()
    {
        ContentType = file.MediaType,
        FileName = file.FileName,
        Stream = stream
    }, default);
    await azureFileService.Delete(file.StorageKey);
    file.FileServiceId = minioConfig.Id;
    file.StorageKey = newStorageKey;
    await db.SaveChangesAsync(default);
    Console.WriteLine("Done");
}
