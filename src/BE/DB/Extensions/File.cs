using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.UrlEncryption;

namespace Chats.BE.DB;

public partial class File
{
    public FileDto ToFileDto(IUrlEncryptionService idEncryption)
    {
        if (FileContentType == null)
        {
            throw new InvalidOperationException("Unable to convert file to DTO: FileContentType is null.");
        }

        return new FileDto
        {
            Id = idEncryption.EncryptFileId(Id),
            FileName = FileName,
            ContentType = FileContentType.ContentType,
        };
    }
}
