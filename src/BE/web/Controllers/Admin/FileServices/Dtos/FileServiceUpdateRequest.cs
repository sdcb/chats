using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services.Common;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.FileServices.Dtos;

public record FileServiceUpdateRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fileServiceTypeId")]
    public required DBFileServiceType FileServiceTypeId { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    [JsonPropertyName("configs")]
    public required string Configs { get; init; }

    public void ApplyTo(FileService data)
    {
        data.Name = Name;
        data.FileServiceTypeId = (byte)FileServiceTypeId;
        data.IsDefault = IsDefault;
        if (!data.Configs.IsMaskedEquals(Configs))
        {
            data.Configs = Configs;
        }
    }
}
