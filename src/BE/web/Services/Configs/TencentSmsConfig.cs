using System.Text.Json.Serialization;

namespace Chats.BE.Services.Configs;

public record TencentSmsConfig
{
    [JsonPropertyName("secretId")]
    public required string SecretId { get; init; }

    [JsonPropertyName("secretKey")]
    public required string SecretKey { get; init; }

    [JsonPropertyName("sdkAppId")]
    public required string SdkAppId { get; init; }

    [JsonPropertyName("signName")]
    public required string SignName { get; init; }

    [JsonPropertyName("templateId")]
    public required string TemplateId { get; init; }
}
