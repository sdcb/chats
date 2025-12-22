using Chats.Web.DB;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Users.ApiKeys.Dtos;

public record UpdateApiKeyDto
{
    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("allowEnumerate")]
    public bool? AllowEnumerate { get; init; }

    [JsonPropertyName("allowAllModels")]
    public bool? AllowAllModels { get; init; }

    [JsonPropertyName("expires")]
    public DateTime? Expires { get; init; }

    [JsonPropertyName("models")]
    public short[]? Models { get; init; }

    [JsonPropertyName("isRevoked")]
    public bool? IsRevoked { get; init; }

    public void ApplyTo(UserApiKey apiKey)
    {
        if (Comment is not null) apiKey.Comment = Comment == "" ? null : Comment;
        if (AllowEnumerate is not null) apiKey.AllowEnumerate = AllowEnumerate.Value;
        if (AllowAllModels is not null) apiKey.AllowAllModels = AllowAllModels.Value;
        if (Expires is not null) apiKey.Expires = Expires.Value;
        if (Models is not null) apiKey.Models = Models.Select(x => new Model { Id = x }).ToList();
        if (IsRevoked is not null) apiKey.IsRevoked = IsRevoked.Value;
    }
}
