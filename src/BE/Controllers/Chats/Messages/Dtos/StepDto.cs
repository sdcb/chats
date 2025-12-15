using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Messages.Dtos;

public record StepDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("edited")]
    public required bool Edited { get; init; }

    [JsonPropertyName("contents")]
    public required ContentResponseItem[] Contents { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    public static StepDto FromDB(Step step, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return new StepDto
        {
            Id = urlEncryption.EncryptStepId(step.Id),
            Edited = step.Edited,
            Contents = ContentResponseItem.FromContent([.. step.StepContents], fup, urlEncryption),
            CreatedAt = step.CreatedAt,
        };
    }

    public static StepDto[] FromDB(Step[] steps, FileUrlProvider fup, IUrlEncryptionService urlEncryption)
    {
        return [.. steps.Select(x => FromDB(x, fup, urlEncryption))];
    }
}
