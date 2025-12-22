using Chats.Web.DB;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Prompts.Dtos;

public record CreatePromptDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("temperature")]
    public required float? Temperature { get; init; }

    [JsonPropertyName("isSystem")]
    public bool IsSystem { get; init; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; init; }

    public void ApplyTo(Prompt db, bool isAdmin)
    {
        db.IsDefault = IsDefault;
        db.IsSystem = isAdmin && IsSystem;
        db.Name = Name;
        db.Content = Content;
        db.Temperature = Temperature;
    }

    public Prompt ToPrompt(int createUserId, bool isAdmin)
    {
        Prompt theNew = new()
        {
            CreateUserId = createUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyTo(theNew, isAdmin);
        return theNew;
    }
}