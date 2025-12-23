using Chats.DB;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Prompts.Dtos;

public record PromptDto : BriefPromptDto
{
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("temperature")]
    public required float? Temperature { get; init; }

    public void ApplyTo(Prompt db, bool isAdmin)
    {
        db.IsDefault = IsDefault;
        db.IsSystem = isAdmin && IsSystem;
        db.Name = Name;
        db.Content = Content;
    }
}