using Amazon;
using Amazon.S3;
using System.Text.Json.Serialization;

namespace Chats.Web.Services.FileServices.Implementations.AwsS3;

public record AwsS3Config
{
    [JsonPropertyName("region")]
    public string? Region { get; init; }

    [JsonPropertyName("accessKeyId")]
    public string? AccessKeyId { get; init; }

    [JsonPropertyName("secretAccessKey")]
    public string? SecretAccessKey { get; init; }

    [JsonPropertyName("sessionToken")]
    public string? SessionToken { get; init; }

    [JsonPropertyName("bucket")]
    public required string Bucket { get; init; }

    public AmazonS3Client CreateS3()
    {
        if (SecretAccessKey != null)
        {
            if (SessionToken != null)
            {
                // for developers temporary credentials
                return new(AccessKeyId, SecretAccessKey, SessionToken, new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(Region)
                });
            }
            else
            {
                // for server permanent credentials
                return new(AccessKeyId, SecretAccessKey, new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(Region)
                });
            }
        }

        // if it's null, then auto load from default environment profile
        return new();
    }
}