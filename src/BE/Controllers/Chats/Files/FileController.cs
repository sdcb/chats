using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services.ImageInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Microsoft.Net.Http.Headers;
using Chats.BE.Controllers.Common.Dtos;

namespace Chats.BE.Controllers.Chats.Files;

[Route("api"), Authorize]
public class FileController(ChatsDB db, FileServiceFactory fileServiceFactory, IUrlEncryptionService urlEncryption, ILogger<FileController> logger) : ControllerBase
{
    [Route("file-service/upload"), HttpPut]
    public async Task<ActionResult<FileDto>> DefaultUpload(IFormFile file,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fdup,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileContentTypeService fileContentTypeService,
        [FromServices] FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken)
    {
        DB.FileService? fileService = await DB.FileService.GetDefault(db, cancellationToken);
        if (fileService == null)
        {
            return NotFound("File service config not found.");
        }

        return await UploadPrivate(db, file, fileServiceFactory, logger, clientInfoManager, fdup, currentUser, fileService, fileContentTypeService, fileImageInfoService, cancellationToken);
    }

    [Route("file-service/{fileServiceId:int}/upload"), HttpPut]
    public async Task<ActionResult<FileDto>> Upload(int fileServiceId, IFormFile file,
        [FromServices] ClientInfoManager clientInfoManager,
        [FromServices] FileUrlProvider fdup,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileContentTypeService fileContentTypeService,
        [FromServices] FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken)
    {
        DB.FileService? fileService = await db.FileServices.FindAsync([fileServiceId], cancellationToken);
        if (fileService == null)
        {
            return NotFound("File server config not found.");
        }

        return await UploadPrivate(db, file, fileServiceFactory, logger, clientInfoManager, fdup, currentUser, fileService, fileContentTypeService, fileImageInfoService, cancellationToken);
    }

    private async Task<ActionResult<FileDto>> UploadPrivate(ChatsDB db, IFormFile file,
        FileServiceFactory fileServiceFactory,
        ILogger<FileController> logger,
        ClientInfoManager clientInfoManager,
        FileUrlProvider fdup,
        CurrentUser currentUser,
        DB.FileService fileService,
        FileContentTypeService fileContentTypeService,
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
        using Stream baseStream = file.OpenReadStream();
        using PartialBufferedStream pbStream = new(baseStream, 4 * 1024);
        string storageKey = await fs.Upload(new FileUploadRequest
        {
            ContentType = file.ContentType,
            Stream = pbStream,
            FileName = file.FileName
        }, cancellationToken);
        DB.File dbFile = new()
        {
            FileName = file.FileName,
            FileContentType = await fileContentTypeService.GetOrCreate(file.ContentType, cancellationToken),
            FileServiceId = fileService.Id,
            StorageKey = storageKey,
            Size = (int)file.Length,
            ClientInfo = await clientInfoManager.GetClientInfo(cancellationToken),
            CreateUserId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            FileImageInfo = fileImageInfoService.GetImageInfo(file.FileName, file.ContentType, pbStream.SeekedBytes)
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
        DB.File? file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.FileContentType)
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
        DB.File? file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.FileContentType)
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);
        if (file == null)
        {
            return NotFound("File not found.");
        }

        return ServeStaticFile(file);
    }

    internal ActionResult ServeStaticFile(DB.File file)
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
            return PhysicalFile(fileInfo.FullName, file.FileContentType.ContentType, lastModified, etag, enableRangeProcessing: true);
        }
        else
        {
            Uri downloadUrl = fs.CreateDownloadUrl(CreateDownloadUrlRequest.FromFile(file));
            return Redirect(downloadUrl.ToString());
        }
    }

    [HttpGet("file")]
    public async Task<ActionResult<PagedResult<FileDto>>> QueryFiles(PagingRequest query,
        [FromServices] CurrentUser currentUser,
        [FromServices] FileUrlProvider fdup,
        CancellationToken cancellationToken)
    {
        IQueryable<DB.File> queryable = db.Files
            .Where(x => x.CreateUserId == currentUser.Id)
            .OrderByDescending(x => x.Id);
        PagedResult<FileDto> pagedResult = await PagedResult.FromTempQuery(queryable, query, fdup.CreateFileDto, cancellationToken);
        return Ok(pagedResult);
    }

    [HttpDelete("file/{encryptedFileId}")]
    public async Task<ActionResult> DeleteFile(string encryptedFileId,
        [FromServices] CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        int fileId = urlEncryption.DecryptFileId(encryptedFileId);
        DB.File? file = await db.Files
            .Include(x => x.FileService)
            .Include(x => x.MessageContentFiles)
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

        if (file.MessageContentFiles.Count != 0)
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
