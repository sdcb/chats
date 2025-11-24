using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Controllers.Admin.AdminModels.Validators;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

[ValidateTemperatureRange]
[ValidateChatResponseTokens]
[ValidateImageSizes]
[ValidateImageBatchCount]
[ValidateMaxThinkingBudget]
public record UpdateModelRequest
{
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public required string Name { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("deploymentName")]
    [Required(ErrorMessage = "Deployment name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Deployment name must be between 1 and 100 characters")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("modelKeyId")]
    [Range(1, short.MaxValue, ErrorMessage = "Model key ID must be greater than 0")]
    public required short ModelKeyId { get; init; }

    [JsonPropertyName("inputTokenPrice1M")]
    [Range(0, double.MaxValue, ErrorMessage = "Input token price must be non-negative")]
    public required decimal InputTokenPrice1M { get; init; }

    [JsonPropertyName("outputTokenPrice1M")]
    [Range(0, double.MaxValue, ErrorMessage = "Output token price must be non-negative")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("allowSearch")]
    public required bool AllowSearch { get; init; }

    [JsonPropertyName("allowVision")]
    public required bool AllowVision { get; init; }

    [JsonPropertyName("supportsVisionLink")]
    public required bool SupportsVisionLink { get; init; }

    [JsonPropertyName("allowStreaming")]
    public required bool AllowStreaming { get; init; }

    [JsonPropertyName("allowCodeExecution")]
    public required bool AllowCodeExecution { get; init; }

    [JsonPropertyName("reasoningEffortOptions")]
    public required int[] ReasoningEffortOptions { get; init; }

    [JsonPropertyName("minTemperature")]
    [Range(0, 2, ErrorMessage = "Minimum temperature must be between 0 and 2")]
    public required decimal MinTemperature { get; init; }

    [JsonPropertyName("maxTemperature")]
    [Range(0, 2, ErrorMessage = "Maximum temperature must be between 0 and 2")]
    public required decimal MaxTemperature { get; init; }

    [JsonPropertyName("contextWindow")]
    [Range(0, int.MaxValue, ErrorMessage = "Context window must be non-negative")]
    public required int ContextWindow { get; init; }

    [JsonPropertyName("maxResponseTokens")]
    [Range(0, int.MaxValue, ErrorMessage = "Max response tokens must be non-negative")]
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
    public int? MaxThinkingBudget { get; init; }

    public void ApplyTo(Model cm)
    {
        cm.Name = Name;
        cm.IsDeleted = !Enabled;
        cm.ModelKeyId = ModelKeyId;
        cm.InputTokenPrice1M = InputTokenPrice1M;
        cm.OutputTokenPrice1M = OutputTokenPrice1M;
        cm.DeploymentName = DeploymentName;
        cm.AllowSearch = AllowSearch;
        cm.AllowVision = AllowVision;
        cm.SupportsVisionLink = SupportsVisionLink;
        cm.AllowStreaming = AllowStreaming;
        cm.AllowCodeExecution = AllowCodeExecution;
        cm.ReasoningEffortOptions = ReasoningEffortOptions.Length > 0 ? string.Join(',', ReasoningEffortOptions) : null;
        cm.MinTemperature = MinTemperature;
        cm.MaxTemperature = MaxTemperature;
        cm.ContextWindow = ContextWindow;
        cm.MaxResponseTokens = MaxResponseTokens;
        cm.AllowToolCall = AllowToolCall;
        cm.SupportedImageSizes = SupportedImageSizes.Length > 0 ? string.Join(',', SupportedImageSizes) : null;
        cm.ApiTypeId = (byte)ApiType;
        cm.UseAsyncApi = UseAsyncApi;
        cm.UseMaxCompletionTokens = UseMaxCompletionTokens;
        cm.IsLegacy = IsLegacy;
        cm.ThinkTagParserEnabled = ThinkTagParserEnabled;
        cm.MaxThinkingBudget = MaxThinkingBudget;
    }
}
