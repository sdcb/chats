using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Microsoft.Net.Http.Headers;
using Chats.BE.Controllers.Common.Dtos;
using System.Runtime.Caching;
using Chats.DB;
using DBFile = Chats.DB.File;
using Chats.DB.Enums;

namespace Chats.BE.Controllers.Chats.Files;

[Route("api"), Authorize]
public class FileController(ChatsDB db, IFileServiceFactory fileServiceFactory, IUrlEncryptionService urlEncryption, ILogger<FileController> logger) : ControllerBase
{
    [Route("file-service/upload"), HttpPut]
    public async Task<ActionResult<FileDto>> DefaultUpload(IFormFile file,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fdup,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken)
    {
        FileService? fileService = await db.GetDefaultFileService(cancellationToken);
        if (fileService == null)
        {
            return NotFound("File service config not found.");
        }

        return await UploadPrivate(db, file, fileServiceFactory, logger, clientInfoManager, fdup, currentUser, fileService, fileImageInfoService, cancellationToken);
    }

    [Route("file-service/{fileServiceId:int}/upload"), HttpPut]
    public async Task<ActionResult<FileDto>> Upload(int fileServiceId, IFormFile file,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fdup,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken)
    {
        FileService? fileService = await db.FileServices.FindAsync([fileServiceId], cancellationToken);
        if (fileService == null)
        {
            return NotFound("File server config not found.");
        }

        return await UploadPrivate(db, file, fileServiceFactory, logger, clientInfoManager, fdup, currentUser, fileService, fileImageInfoService, cancellationToken);
    }

    private async Task<ActionResult<FileDto>> UploadPrivate(ChatsDB db, IFormFile file,
        IFileServiceFactory fileServiceFactory,
        ILogger<FileController> logger,
        ClientInfoManager clientInfoManager,
        FileUrlProvider fdup,
        CurrentUser currentUser,
        FileService fileService,
        FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest("File is empty.");
        }
        if (file.Length > 15 * 1024 * 1024)
        {
            return BadRequest("File is too large.");
        }
        if (!string.IsNullOrWhiteSpace(file.FileName) && file.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
        {
            return BadRequest("Invalid file name.");
        }

        if (!fileService.IsDefault && !currentUser.IsAdmin)
        {
            // only admin can upload to non-default file service
            return NotFound("File service config not found.");
        }

        IFileService fs = fileServiceFactory.Create(fileService);
        
        // Read file to memory for parsing and uploading
        byte[] fileBytes;
        using (Stream baseStream = file.OpenReadStream())
        {
            using MemoryStream ms = new();
            await baseStream.CopyToAsync(ms, cancellationToken);
            fileBytes = ms.ToArray();
        }

        // Get image info before upload (for images only)
        FileImageInfo? imageInfo = fileImageInfoService.GetImageInfo(
            file.FileName, 
            file.ContentType, 
            fileBytes);

        // Upload file
        string storageKey;
        using (MemoryStream uploadStream = new(fileBytes))
        {
            storageKey = await fs.Upload(new FileUploadRequest
            {
                ContentType = file.ContentType,
                Stream = uploadStream,
                FileName = file.FileName
            }, cancellationToken);
        }

        DBFile dbFile = new()
        {
            FileName = file.FileName,
            MediaType = file.ContentType,
            FileServiceId = fileService.Id,
            StorageKey = storageKey,
            Size = (int)file.Length,
            ClientInfo = await clientInfoManager.GetClientInfo(cancellationToken),
            CreateUserId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            FileImageInfo = imageInfo
        };
        db.Files.Add(dbFile);
        await db.SaveChangesAsync(cancellationToken);

        FileDto fileDto = fdup.CreateFileDto(dbFile);
        return Created(default(string), value: fileDto);
    }

    [Route("file/private/{encryptedFileId}"), HttpGet]
    public async Task<ActionResult> DownloadPrivate(string encryptedFileId,
        [FromServices] CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        int fileId = urlEncryption.DecryptFileId(encryptedFileId);
        DBFile? file = await db.Files
            .Include(x => x.FileService)
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);
        if (file == null)
        {
            return NotFound("File not found.");
        }
        if (file.CreateUserId != currentUser.Id && !currentUser.IsAdmin)
        {
            // only the creator or admin can download the file
            return NotFound("File not found.");
        }

        return ServeStaticFile(file);
    }

    [HttpGet("file/{encryptedFileId}"), AllowAnonymous]
    public async Task<ActionResult> DownloadPublic(string encryptedFileId, long validBefore, string hash, CancellationToken cancellationToken)
    {
        Result<int> decodeResult = urlEncryption.DecodeFileIdPath(encryptedFileId, validBefore, hash);
        if (decodeResult.IsFailure)
        {
            return BadRequest(decodeResult.Error);
        }

        int fileId = decodeResult.Value;
        DBFile? file = await db.Files
            .Include(x => x.FileService)
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);
        if (file == null)
        {
            return NotFound("File not found.");
        }

        return ServeStaticFile(file);
    }

    private static readonly MemoryCache _fileUrlCache = new("file-url-cache");

    internal ActionResult ServeStaticFile(DBFile file)
    {
        DBFileServiceType fileServiceType = (DBFileServiceType)file.FileService.FileServiceTypeId;
        IFileService fs = fileServiceFactory.Create(file.FileService);
        if (fileServiceType == DBFileServiceType.Local)
        {
            FileInfo fileInfo = new(Path.Combine(file.FileService.Configs, file.StorageKey));
            if (!fileInfo.Exists)
            {
                return NotFound("File not found.");
            }

            DateTimeOffset lastModified = fileInfo.LastWriteTimeUtc;
            EntityTagHeaderValue etag = new('"' + lastModified.Ticks.ToString("x") + '"', isWeak: true);
            Response.Headers[HeaderNames.ContentDisposition] = $"inline; filename=\"{file.FileName}\"";
            return PhysicalFile(fileInfo.FullName, file.MediaType, lastModified, etag, enableRangeProcessing: true);
        }
        else
        {
            if (_fileUrlCache.Get(file.Id.ToString()) is string cachedUrl)
            {
                return Redirect(cachedUrl);
            }
            else
            {
                CreateDownloadUrlRequest request = CreateDownloadUrlRequest.FromFile(file);
                string downloadUrl = fs.CreateDownloadUrl(request);
                _fileUrlCache.Set(file.Id.ToString(), downloadUrl, new CacheItemPolicy { AbsoluteExpiration = request.ValidEnd });
                return Redirect(downloadUrl.ToString());
            }
        }
    }

    [HttpGet("file")]
    public async Task<ActionResult<PagedResult<FileDto>>> QueryFiles(PagingRequest query,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileUrlProvider fdup,
        CancellationToken cancellationToken)
    {
        IQueryable<DBFile> queryable = db.Files
            .Where(x => x.CreateUserId == currentUser.Id)
            .OrderByDescending(x => x.Id);
        PagedResult<FileDto> pagedResult = await PagedResult.FromTempQuery(queryable, query, f => fdup.CreateFileDto(f), cancellationToken);
        return Ok(pagedResult);
    }

    [HttpDelete("file/{encryptedFileId}")]
    public async Task<ActionResult> DeleteFile(string encryptedFileId,
        [FromServices] CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        int fileId = urlEncryption.DecryptFileId(encryptedFileId);
        DBFile? file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.StepContentFiles)
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);
        if (file == null)
        {
            return NotFound("File not found.");
        }
        if (file.CreateUserId != currentUser.Id && !currentUser.IsAdmin)
        {
            // only the creator or admin can delete the file
            return NotFound("File not found.");
        }

        if (file.StepContentFiles.Count != 0)
        {
            return BadRequest("File is used in messages, cannot delete.");
        }

        IFileService fs = fileServiceFactory.Create(file.FileService);
        bool deleted = await fs.Delete(file.StorageKey, cancellationToken);
        if (!deleted)
        {
            logger.LogWarning("Failed to delete file {FileId} from file service {FileServiceId}", file.Id, file.FileServiceId);
        }
        db.Files.Remove(file);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
