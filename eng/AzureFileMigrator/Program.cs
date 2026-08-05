using AzureFileMigrator;
using Azure;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using DBFile = Chats.DB.File;

MigrationOptions options = MigrationOptions.Parse(args);

if (options.ShowHelp)
{
    MigrationOptions.PrintHelp();
    return;
}

Console.WriteLine(options.DryRun
    ? "Running in DRY-RUN mode. No files will be uploaded/deleted and database will not be changed."
    : "Running in EXECUTE mode. Files may be uploaded and database may be changed.");

if (!options.DryRun)
{
    Console.WriteLine("Press enter to start the migration...");
    Console.ReadLine();
}

IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string chatsConnectionString = configuration.GetConnectionString("ChatsDB")
    ?? throw new InvalidOperationException("ConnectionStrings:ChatsDB is missing.");

IHostEnvironment hostEnvironment = new HostingEnvironment
{
    EnvironmentName = "Development",
    ApplicationName = "AzureFileMigrator",
    ContentRootPath = Directory.GetCurrentDirectory()
};

await using ChatsDB db = new(new DbContextOptionsBuilder<ChatsDB>()
    .UseSqlServer(chatsConnectionString)
    .Options);

FileService[] interestedFileServices = db.FileServices
    .Where(x => x.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage || x.FileServiceTypeId == (byte)DBFileServiceType.Minio)
    .ToArray();

FileService azureConfig = interestedFileServices.Single(x => x.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage);
FileService minioConfig = interestedFileServices.Single(x => x.FileServiceTypeId == (byte)DBFileServiceType.Minio);

Console.WriteLine($"AzureBlobStorage FileServiceId: {azureConfig.Id}");
Console.WriteLine($"Minio FileServiceId: {minioConfig.Id}");

FileServiceFactory fileServiceFactory = new(new DummyHostUrlService(), new NoOpUrlEncryptionService());
IFileService azureFileService = fileServiceFactory.Create(azureConfig);
IFileService minioFileService = fileServiceFactory.Create(minioConfig);

if (options.ProbeMinio)
{
    int sample = options.Sample ?? 20;
    Console.WriteLine($"MinIO probe sample: {sample}");
    List<DBFile> minioFiles = await db.Files
        .AsNoTracking()
        .Where(x => x.FileServiceId == minioConfig.Id)
        .OrderBy(x => Guid.NewGuid())
        .Take(sample)
        .ToListAsync();

    long minioSuccess = 0;
    long minioFailed = 0;
    foreach (DBFile file in minioFiles)
    {
        try
        {
            Console.Write($"Probing MinIO id={file.Id}, name={file.FileName}, key={file.StorageKey}...");
            await using Stream stream = await minioFileService.Download(file.StorageKey);
            byte[] buffer = new byte[1];
            _ = await stream.ReadAsync(buffer);
            minioSuccess++;
            Console.WriteLine("Readable");
        }
        catch (Exception ex)
        {
            minioFailed++;
            Console.WriteLine($"Failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    Console.WriteLine($"MinIO probe done. Success={minioSuccess}, Failed={minioFailed}, Sampled={minioFiles.Count}");
    return;
}

IQueryable<DBFile> query = db.Files
    .AsNoTracking()
    .Include(x => x.FileService)
    .Where(x => x.FileService.FileServiceTypeId == (byte)DBFileServiceType.AzureBlobStorage);

int totalAzureFiles = await query.CountAsync();
Console.WriteLine($"Azure-referenced files in DB: {totalAzureFiles}");
Console.WriteLine($"Order: {(options.OldestFirst ? "oldest first (Id ASC)" : "newest first (Id DESC)")}");

query = options.OldestFirst
    ? query.OrderBy(x => x.Id)
    : query.OrderByDescending(x => x.Id);

if (options.Limit is not null)
{
    query = query.Take(options.Limit.Value);
    Console.WriteLine($"Limit: {options.Limit.Value}");
}

List<DBFile> files = await query.ToListAsync();
Console.WriteLine($"Files selected: {files.Count}");

long success = 0;
long failed = 0;
long missing = 0;
long totalBytes = 0;
long processed = 0;
object consoleLock = new();

if (options.DryRun && options.ProbeSource && options.Degree > 1)
{
    Console.WriteLine($"Probe degree: {options.Degree}");
    await Parallel.ForEachAsync(
        files.Select((file, index) => new IndexedFile(index, file)),
        new ParallelOptions { MaxDegreeOfParallelism = options.Degree },
        async (item, cancellationToken) =>
        {
            DBFile file = item.File;
            try
            {
                await using Stream probeStream = await azureFileService.Download(file.StorageKey, cancellationToken);
                long? length = probeStream.CanSeek ? probeStream.Length : null;
                if (length is not null)
                {
                    Interlocked.Add(ref totalBytes, length.Value);
                }

                long currentSuccess = Interlocked.Increment(ref success);
                long currentProcessed = Interlocked.Increment(ref processed);

                if (options.Verbose)
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine(length is null
                            ? $"Processing {item.Index + 1}/{files.Count}: id={file.Id}, key={file.StorageKey}...Azure readable"
                            : $"Processing {item.Index + 1}/{files.Count}: id={file.Id}, key={file.StorageKey}...Azure readable, {Format.Bytes(length.Value)}");
                    }
                }
                else if (currentProcessed % 100 == 0 || currentProcessed == files.Count)
                {
                    Console.WriteLine($"Probed {currentProcessed}/{files.Count}, success={currentSuccess}, failed={Volatile.Read(ref failed)}, bytes={Format.Bytes(Volatile.Read(ref totalBytes))}");
                }
            }
            catch (Exception ex)
            {
                if (options.SkipMissingSource && IsBlobNotFound(ex))
                {
                    long currentMissing = Interlocked.Increment(ref missing);
                    long missingProcessed = Interlocked.Increment(ref processed);
                    lock (consoleLock)
                    {
                        Console.WriteLine($"Missing {missingProcessed}/{files.Count}: id={file.Id}, key={file.StorageKey}: {ex.Message}");
                    }

                    if (!options.Verbose && (missingProcessed % 100 == 0 || missingProcessed == files.Count))
                    {
                        Console.WriteLine($"Probed {missingProcessed}/{files.Count}, success={Volatile.Read(ref success)}, failed={Volatile.Read(ref failed)}, missing={currentMissing}, bytes={Format.Bytes(Volatile.Read(ref totalBytes))}");
                    }

                    return;
                }

                long currentFailed = Interlocked.Increment(ref failed);
                long failedProcessed = Interlocked.Increment(ref processed);
                lock (consoleLock)
                {
                    Console.WriteLine($"Failed {failedProcessed}/{files.Count}: id={file.Id}, key={file.StorageKey}: {ex.GetType().Name}: {ex.Message}");
                }

                if (!options.Verbose && (failedProcessed % 100 == 0 || failedProcessed == files.Count))
                {
                    Console.WriteLine($"Probed {failedProcessed}/{files.Count}, success={Volatile.Read(ref success)}, failed={currentFailed}, missing={Volatile.Read(ref missing)}, bytes={Format.Bytes(Volatile.Read(ref totalBytes))}");
                }
            }
        });
}
else if (!options.DryRun && options.Degree > 1)
{
    Console.WriteLine($"Execute degree: {options.Degree}");
    await Parallel.ForEachAsync(
        files.Select((file, index) => new IndexedFile(index, file)),
        new ParallelOptions { MaxDegreeOfParallelism = options.Degree },
        async (item, cancellationToken) =>
        {
            DBFile file = item.File;
            try
            {
                await using Stream stream = await azureFileService.Download(file.StorageKey, cancellationToken);
                string newStorageKey = await minioFileService.Upload(new FileUploadRequest
                {
                    ContentType = file.MediaType,
                    FileName = file.FileName,
                    Stream = stream
                }, cancellationToken);

                await using ChatsDB updateDb = new(new DbContextOptionsBuilder<ChatsDB>()
                    .UseSqlServer(chatsConnectionString)
                    .Options);
                DBFile trackedFile = await updateDb.Files.SingleAsync(x => x.Id == file.Id, cancellationToken);
                if (trackedFile.FileServiceId != azureConfig.Id)
                {
                    long skippedProcessed = Interlocked.Increment(ref processed);
                    if (options.Verbose)
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine($"Skipped {skippedProcessed}/{files.Count}: id={file.Id}, already FileServiceId={trackedFile.FileServiceId}");
                        }
                    }

                    return;
                }

                trackedFile.FileServiceId = minioConfig.Id;
                trackedFile.StorageKey = newStorageKey;
                await updateDb.SaveChangesAsync(cancellationToken);

                if (options.DeleteSource)
                {
                    _ = await azureFileService.Delete(file.StorageKey, cancellationToken);
                }

                long currentSuccess = Interlocked.Increment(ref success);
                long currentProcessed = Interlocked.Increment(ref processed);
                if (options.Verbose)
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine($"Migrated {currentProcessed}/{files.Count}: id={file.Id}, newKey={newStorageKey}");
                    }
                }
                else if (currentProcessed % 100 == 0 || currentProcessed == files.Count)
                {
                    Console.WriteLine($"Migrated {currentProcessed}/{files.Count}, success={currentSuccess}, failed={Volatile.Read(ref failed)}, missing={Volatile.Read(ref missing)}");
                }
            }
            catch (Exception ex)
            {
                if (options.SkipMissingSource && IsBlobNotFound(ex))
                {
                    long currentMissing = Interlocked.Increment(ref missing);
                    long missingProcessed = Interlocked.Increment(ref processed);
                    lock (consoleLock)
                    {
                        Console.WriteLine($"Missing {missingProcessed}/{files.Count}: id={file.Id}, key={file.StorageKey}: {ex.Message}");
                    }

                    if (!options.Verbose && (missingProcessed % 100 == 0 || missingProcessed == files.Count))
                    {
                        Console.WriteLine($"Migrated {missingProcessed}/{files.Count}, success={Volatile.Read(ref success)}, failed={Volatile.Read(ref failed)}, missing={currentMissing}");
                    }

                    return;
                }

                long currentFailed = Interlocked.Increment(ref failed);
                long failedProcessed = Interlocked.Increment(ref processed);
                lock (consoleLock)
                {
                    Console.WriteLine($"Failed {failedProcessed}/{files.Count}: id={file.Id}, key={file.StorageKey}: {ex.GetType().Name}: {ex.Message}");
                }

                if (!options.Verbose && (failedProcessed % 100 == 0 || failedProcessed == files.Count))
                {
                    Console.WriteLine($"Migrated {failedProcessed}/{files.Count}, success={Volatile.Read(ref success)}, failed={currentFailed}, missing={Volatile.Read(ref missing)}");
                }
            }
        });
}
else
{
    for (int i = 0; i < files.Count; i++)
    {
        DBFile file = files[i];
        bool printItem = !options.DryRun || options.ProbeSource || options.Verbose;
        if (printItem)
        {
            Console.Write($"Processing {i + 1}/{files.Count}: id={file.Id}, name={file.FileName}, key={file.StorageKey}...");
        }

        try
        {
            if (options.DryRun)
            {
                if (options.ProbeSource)
                {
                    await using Stream probeStream = await azureFileService.Download(file.StorageKey);
                    long? length = probeStream.CanSeek ? probeStream.Length : null;
                    if (length is not null)
                    {
                        totalBytes += length.Value;
                    }

                    if (printItem)
                    {
                        Console.WriteLine(length is null ? "Azure readable" : $"Azure readable, {Format.Bytes(length.Value)}");
                    }
                }
                else
                {
                    if (printItem)
                    {
                        Console.WriteLine("Skipped");
                    }
                }

                success++;
                continue;
            }

            using Stream stream = await azureFileService.Download(file.StorageKey);
            string newStorageKey = await minioFileService.Upload(new FileUploadRequest()
            {
                ContentType = file.MediaType,
                FileName = file.FileName,
                Stream = stream
            }, default);

            DBFile trackedFile = await db.Files.SingleAsync(x => x.Id == file.Id);
            trackedFile.FileServiceId = minioConfig.Id;
            trackedFile.StorageKey = newStorageKey;
            await db.SaveChangesAsync(default);

            if (options.DeleteSource)
            {
                _ = await azureFileService.Delete(file.StorageKey);
            }

            Console.WriteLine(options.DeleteSource ? "Migrated and deleted source" : "Migrated");
            success++;
        }
        catch (Exception ex)
        {
            if (options.SkipMissingSource && IsBlobNotFound(ex))
            {
                missing++;
                Console.WriteLine($"Missing source, skipped: {ex.Message}");
                continue;
            }

            failed++;
            Console.WriteLine($"Failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

Console.WriteLine();
Console.WriteLine($"Done. Success={success}, Failed={failed}, Missing={missing}, DryRun={options.DryRun}, ProbeSource={options.ProbeSource}, DeleteSource={options.DeleteSource}");
if (options.ProbeSource)
{
    Console.WriteLine($"Total readable bytes: {totalBytes} ({Format.Bytes(totalBytes)})");
}

static bool IsBlobNotFound(Exception ex)
{
    return ex is RequestFailedException { ErrorCode: "BlobNotFound" };
}

internal sealed record MigrationOptions(bool DryRun, bool ProbeSource, bool ProbeMinio, bool DeleteSource, bool SkipMissingSource, bool Verbose, bool OldestFirst, int Degree, int? Limit, int? Sample, bool ShowHelp)
{
    public static MigrationOptions Parse(string[] args)
    {
        bool dryRun = true;
        bool probeSource = false;
        bool probeMinio = false;
        bool deleteSource = false;
        bool skipMissingSource = false;
        bool verbose = false;
        bool oldestFirst = false;
        int degree = 1;
        int? limit = null;
        int? sample = null;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--execute":
                    dryRun = false;
                    break;
                case "--probe-source":
                    probeSource = true;
                    break;
                case "--probe-minio":
                    probeMinio = true;
                    dryRun = true;
                    break;
                case "--delete-source":
                    deleteSource = true;
                    break;
                case "--skip-missing-source":
                    skipMissingSource = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--oldest-first":
                    oldestFirst = true;
                    break;
                case "--newest-first":
                    oldestFirst = false;
                    break;
                case "--degree":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int parsedDegree) || parsedDegree < 1)
                    {
                        throw new ArgumentException("--degree requires a positive integer.");
                    }

                    degree = parsedDegree;
                    break;
                case "--limit":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int parsedLimit) || parsedLimit < 1)
                    {
                        throw new ArgumentException("--limit requires a positive integer.");
                    }

                    limit = parsedLimit;
                    break;
                case "--sample":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int parsedSample) || parsedSample < 1)
                    {
                        throw new ArgumentException("--sample requires a positive integer.");
                    }

                    sample = parsedSample;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (deleteSource && dryRun)
        {
            throw new ArgumentException("--delete-source can only be used with --execute.");
        }

        if (degree > 1 && dryRun && !probeSource)
        {
            throw new ArgumentException("--degree > 1 in dry-run mode is only supported with --probe-source.");
        }

        return new MigrationOptions(dryRun, probeSource, probeMinio, deleteSource, skipMissingSource, verbose, oldestFirst, degree, limit, sample, showHelp);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
        AzureFileMigrator

        Defaults to dry-run mode.

        Options:
          --dry-run          Inspect database Azure file references only. This is the default.
          --probe-source     In dry-run mode, open each Azure blob and read one byte to verify source readability.
          --probe-minio      Randomly sample MinIO-backed DB files and read one byte.
          --sample <n>       Sample size for --probe-minio. Default: 20.
          --degree <n>       Parallelism for --dry-run --probe-source or --execute. Example: --degree 16.
          --skip-missing-source
                             Treat Azure BlobNotFound as skipped/missing instead of failed.
          --oldest-first     Process by Id ASC. Default is newest first, Id DESC.
          --newest-first     Process by Id DESC. This is the default.
          --verbose          Print every selected file.
          --limit <n>        Process at most n files.
          --execute          Run the migration: Azure download -> MinIO upload -> update DB.
          --delete-source    With --execute only, delete Azure source after DB update.
          -h, --help         Show help.
        """);
    }
}

internal sealed record IndexedFile(int Index, DBFile File);

internal static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
