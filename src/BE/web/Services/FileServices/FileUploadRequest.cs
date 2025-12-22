using Microsoft.IdentityModel.Tokens;

namespace Chats.Web.Services.FileServices;

/// <summary>
/// Represents a request to upload a file.
/// </summary>
/// <remarks>
/// The <see cref="Stream"/> property will not be disposed by the file service.
/// </remarks>
public record FileUploadRequest
{
    /// <summary>
    /// Gets the name of the file.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the content type of the file.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the stream containing the file data.
    /// </summary>
    /// <remarks>
    /// The stream will not be disposed by the file service.
    /// </remarks>
    public required Stream Stream { get; init; }
}

public record CreateDownloadUrlRequest
{
    public required int FileId { get; init; }
    public required string StorageKey { get; init; }
    public TimeSpan ValidPeriod { get; init; } = DefaultValidPeriod;
    public DateTimeOffset ValidEnd => DateTimeOffset.UtcNow + ValidPeriod;

    public static TimeSpan DefaultValidPeriod { get; } = TimeSpan.FromHours(2);

    public static CreateDownloadUrlRequest FromFile(DB.File file)
    {
        return new CreateDownloadUrlRequest { FileId = file.Id, StorageKey = file.StorageKey };
    }
}

public record SuggestedStorageInfo(string Folder, string FileName)
{
    public string StorageKey { get; } = $"{Folder}/{FileName}";

    public static SuggestedStorageInfo FromFileName(string fileName)
    {
        DateTime now = DateTime.UtcNow;
        string safeFileName = Path.GetFileName(fileName);
        string randomStuff = Base64UrlEncoder.Encode(Guid.NewGuid().ToByteArray());
        return new SuggestedStorageInfo($"{now:yyyy}/{now:MM}/{now:dd}", $"{randomStuff}-{safeFileName}");
    }
}
