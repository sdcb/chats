using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.UserConfigs.Dtos;

public sealed record TitleSummaryDefaultTemplateDto
{
    [JsonPropertyName("promptTemplate")]
    public required string PromptTemplate { get; init; }
}
