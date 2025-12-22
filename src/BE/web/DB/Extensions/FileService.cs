using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

public partial class FileService
{
    public static async Task<FileService?> GetDefault(ChatsDB db, CancellationToken cancellationToken = default)
    {
        FileService? fileService = await db.FileServices
            .Where(fs => fs.IsDefault)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return fileService;
    }

    public static async Task<int?> GetDefaultId(ChatsDB db, CancellationToken cancellationToken = default)
    {
        int? fileServiceId = await db.FileServices
            .Where(fs => fs.IsDefault)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (fileServiceId == 0) return null;
        return fileServiceId;
    }
}
