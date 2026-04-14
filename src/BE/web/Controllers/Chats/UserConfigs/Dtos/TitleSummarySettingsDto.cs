using Chats.BE.Services.TitleSummary;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.UserConfigs.Dtos;

public sealed record TitleSummarySettingsDto
{
    [JsonPropertyName("adminConfig")]
    public TitleSummaryConfig? AdminConfig { get; init; }

    [JsonPropertyName("userConfig")]
    public TitleSummaryConfig? UserConfig { get; init; }

    [JsonPropertyName("resolvedConfig")]
    public required ResolvedTitleSummaryConfig ResolvedConfig { get; init; }
}
