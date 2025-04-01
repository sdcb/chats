using Chats.BE.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.FileServices;

public class FileContentTypeService(ChatsDB db)
{
    public async Task<FileContentType> GetOrCreate(string contentType, CancellationToken cancellationToken)
    {
        FileContentType? dbContentType = await db.FileContentTypes.FirstOrDefaultAsync(x => x.ContentType == contentType, cancellationToken);
        if (dbContentType == null)
        {
            dbContentType = new FileContentType
            {
                ContentType = contentType
            };
            db.FileContentTypes.Add(dbContentType);
            await db.SaveChangesAsync(cancellationToken);
        }
        return dbContentType;
    }
}
