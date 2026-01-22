using Chats.DB.Enums;
using Chats.BE.Services.Common;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.FileServices.Dtos;

public record FileServiceSimpleDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public record FileServiceDto : FileServiceSimpleDto
{
    [JsonPropertyName("fileServiceTypeId")]
    public required DBFileServiceType FileServiceTypeId { get; init; }

    [JsonPropertyName("configs")]
    public required string Configs { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    [JsonPropertyName("fileCount")]
    public required int FileCount { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }

    public FileServiceDto WithMaskedKeys() => this with 
    { 
        Configs = FileServiceTypeId switch
        { 
            DBFileServiceType.Local => Configs, 
            _ => Configs.JsonToMaskedNull()!
        }
    };
}
