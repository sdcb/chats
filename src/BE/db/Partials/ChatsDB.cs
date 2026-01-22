using Chats.BE.DB.Converter;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

public partial class ChatsDB
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeAsUtcValueConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableDateTimeAsUtcValueConverter>();
    }

    public async Task<FileService?> GetDefaultFileService(CancellationToken cancellationToken = default)
    {
        FileService? fileService = await FileServices
            .Where(fs => fs.IsDefault)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return fileService;
    }
}
