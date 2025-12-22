using Chats.Web.DB;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Messages.Dtos;

/// <summary>
/// 表示一个 Step，包含内容、编辑状态等信息
/// </summary>
public record StepDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("contents")]
    public required ContentResponseItem[] Contents { get; init; }

    [JsonPropertyName("edited")]
    public required bool Edited { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    public static StepDto FromDB(Step step, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return new StepDto
        {
            Id = urlEncryption.EncryptStepId(step.Id),
            Contents = ContentResponseItem.FromContent([.. step.StepContents.OrderBy(x => x.Id)], fup, urlEncryption),
            Edited = step.Edited,
            CreatedAt = step.CreatedAt,
        };
    }

    public static StepDto[] FromDB(IEnumerable<Step> steps, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return [.. steps.OrderBy(x => x.Id).Select(s => FromDB(s, fup, urlEncryption))];
    }
}
