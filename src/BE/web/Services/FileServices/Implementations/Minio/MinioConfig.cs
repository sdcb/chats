using Amazon;
using Amazon.S3;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.FileServices.Implementations.Minio;

public record MinioConfig
{
    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("accessKey")]
    public required string AccessKey { get; init; }

    [JsonPropertyName("secretKey")]
    public required string SecretKey { get; init; }

    [JsonPropertyName("bucket")]
    public required string Bucket { get; init; }

    [JsonPropertyName("region")]
    public string? Region { get; init; } // Nullable for the default null value.

    public AmazonS3Client CreateS3()
    {
        AmazonS3Config s3Config = new()
        {
            ForcePathStyle = true,
            ServiceURL = Endpoint,
        };
        if (Region != null)
        {
            s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(Region);
        }
        AmazonS3Client s3 = new(AccessKey, SecretKey, s3Config);
        return s3;
    }
}