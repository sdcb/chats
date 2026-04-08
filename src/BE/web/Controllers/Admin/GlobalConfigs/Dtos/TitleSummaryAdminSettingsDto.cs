using Chats.BE.Services.TitleSummary;

namespace Chats.BE.Controllers.Admin.GlobalConfigs.Dtos;

public sealed class TitleSummaryAdminSettingsDto
{
    public TitleSummaryConfig? Config { get; init; }

    public required string DefaultPromptTemplate { get; init; }
}
