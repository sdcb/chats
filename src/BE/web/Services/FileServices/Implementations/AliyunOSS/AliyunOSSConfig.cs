using System.Text.Json.Serialization;

namespace Chats.Web.Services.FileServices.Implementations.AliyunOSS;

public record AliyunOssConfig
{
    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("accessKeyId")]
    public required string AccessKeyId { get; init; }

    [JsonPropertyName("accessKeySecret")]
    public required string AccessKeySecret { get; init; }

    [JsonPropertyName("bucket")]
    public required string Bucket { get; init; }
}