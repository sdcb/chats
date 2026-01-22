using Chats.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record AdminModelDto
{
    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    [JsonPropertyName("modelProviderId")]
    public required short ModelProviderId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("modelKeyId")]
    public required short ModelKeyId { get; init; }

    [JsonPropertyName("deploymentName")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("allowSearch")]
    public required bool AllowSearch { get; init; }

    [JsonPropertyName("allowVision")]
    public required bool AllowVision { get; init; }

    [JsonPropertyName("allowCodeExecution")]
    public required bool AllowCodeExecution { get; init; }

    [JsonPropertyName("reasoningEffortOptions")]
    public required int[] ReasoningEffortOptions { get; init; }

    [JsonPropertyName("allowStreaming")]
    public required bool AllowStreaming { get; init; }

    [JsonPropertyName("minTemperature")]
    public required decimal MinTemperature { get; init; }

    [JsonPropertyName("maxTemperature")]
    public required decimal MaxTemperature { get; init; }

    [JsonPropertyName("inputFreshTokenPrice1M")]
    public required decimal InputFreshTokenPrice1M { get; init; }

    [JsonPropertyName("outputTokenPrice1M")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("inputCachedTokenPrice1M")]
    public required decimal InputCachedTokenPrice1M { get; init; }

    [JsonPropertyName("contextWindow")]
    public required int ContextWindow { get; init; }

    [JsonPropertyName("maxResponseTokens")]
    public required int MaxResponseTokens { get; init; }

    [JsonPropertyName("allowToolCall")]
    public required bool AllowToolCall { get; init; }

    [JsonPropertyName("supportedImageSizes")]
    public required string[] SupportedImageSizes { get; init; }

    [JsonPropertyName("apiType")]
    public required DBApiType ApiType { get; init; }

    [JsonPropertyName("useAsyncApi")]
    public required bool UseAsyncApi { get; init; }

    [JsonPropertyName("useMaxCompletionTokens")]
    public required bool UseMaxCompletionTokens { get; init; }

    [JsonPropertyName("isLegacy")]
    public required bool IsLegacy { get; init; }

    [JsonPropertyName("thinkTagParserEnabled")]
    public required bool ThinkTagParserEnabled { get; init; }

    [JsonPropertyName("maxThinkingBudget")]
    public required int? MaxThinkingBudget { get; init; }

    [JsonPropertyName("supportsVisionLink")]
    public required bool SupportsVisionLink { get; init; }
}
