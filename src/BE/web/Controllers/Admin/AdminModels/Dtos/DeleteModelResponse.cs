using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.AdminModels.Dtos;

public record DeleteModelResponse
{
    [JsonPropertyName("softDeleted")]
    public required bool SoftDeleted { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    public static DeleteModelResponse CreateSoftDeleted() => new()
    {
        SoftDeleted = true,
        Message = "Model is in use and has been disabled instead of deleted."
    };

    public static DeleteModelResponse CreateHardDeleted() => new()
    {
        SoftDeleted = false
    };
}
