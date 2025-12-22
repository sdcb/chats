using System.Text.Json.Serialization;

namespace Chats.Web.Services.FileServices.Implementations.AzureBlobStorage;

public record AzureBlobStorageConfig
{
    [JsonPropertyName("connectionString")]
    public required string ConnectionString { get; init; }

    [JsonPropertyName("containerName")]
    public required string ContainerName { get; init; }
}
