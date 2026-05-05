using Chats.BE.Controllers.Admin.AdminModels.Validators;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

[ValidateTemperatureRange]
[ValidateChatResponseTokens]
[ValidateImageSizes]
[ValidateImageBatchCount]
[ValidateMaxThinkingBudget]
[ValidateCustomOverrides]
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

    [JsonPropertyName("inputFreshTokenPrice1M")]
    [Range(0, double.MaxValue, ErrorMessage = "Input token price must be non-negative")]
    public required decimal InputFreshTokenPrice1M { get; init; }

    [JsonPropertyName("outputTokenPrice1M")]
    [Range(0, double.MaxValue, ErrorMessage = "Output token price must be non-negative")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("inputCachedTokenPrice1M")]
    [Range(0, double.MaxValue, ErrorMessage = "Cache token price must be non-negative")]
    public required decimal InputCachedTokenPrice1M { get; init; }

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

    [JsonPropertyName("supportedEfforts")]
    public required string[] SupportedEfforts { get; init; }

    [JsonPropertyName("supportedFormats")]
    public required string[] SupportedFormats { get; init; }

    [JsonPropertyName("overrideUrl")]
    public string? OverrideUrl { get; init; }

    [JsonPropertyName("customHeaders")]
    [ValidateCustomHeaders]
    public string? CustomHeaders { get; init; }

    [JsonPropertyName("customBody")]
    [ValidateCustomBodyPatch]
    public string? CustomBody { get; init; }

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

    public void ApplyTo(Model model)
    {
        model.Enabled = Enabled;
    }

    public bool Matches(Model model, ModelKey modelKey)
    {
        string? supportedEfforts = ToSupportedEffortsCsv(SupportedEfforts);
        string? supportedFormats = ToCsvOrNull(SupportedFormats);
        string? supportedImageSizes = SupportedImageSizes.Length > 0 ? string.Join(',', SupportedImageSizes) : null;
        ModelSnapshot snapshot = model.CurrentSnapshot;

        return model.Enabled == Enabled
            && snapshot.Name == Name
            && snapshot.DeploymentName == DeploymentName
            && snapshot.ModelKeyId == modelKey.Id
            && snapshot.ModelKeySnapshotId == modelKey.CurrentSnapshotId
            && snapshot.InputFreshTokenPrice1M == InputFreshTokenPrice1M
            && snapshot.OutputTokenPrice1M == OutputTokenPrice1M
            && snapshot.InputCachedTokenPrice1M == InputCachedTokenPrice1M
            && snapshot.AllowSearch == AllowSearch
            && snapshot.AllowVision == AllowVision
            && snapshot.SupportsVisionLink == SupportsVisionLink
            && snapshot.AllowStreaming == AllowStreaming
            && snapshot.AllowCodeExecution == AllowCodeExecution
            && snapshot.SupportedEfforts == supportedEfforts
            && snapshot.SupportedFormats == supportedFormats
            && snapshot.OverrideUrl == NullIfWhiteSpace(OverrideUrl)
            && snapshot.CustomHeaders == NullIfWhiteSpace(CustomHeaders)
            && snapshot.CustomBody == NullIfWhiteSpace(CustomBody)
            && snapshot.MinTemperature == MinTemperature
            && snapshot.MaxTemperature == MaxTemperature
            && snapshot.ContextWindow == ContextWindow
            && snapshot.MaxResponseTokens == MaxResponseTokens
            && snapshot.AllowToolCall == AllowToolCall
            && snapshot.SupportedImageSizes == supportedImageSizes
            && snapshot.ApiTypeId == (byte)ApiType
            && snapshot.UseAsyncApi == UseAsyncApi
            && snapshot.UseMaxCompletionTokens == UseMaxCompletionTokens
            && snapshot.IsLegacy == IsLegacy
            && snapshot.ThinkTagParserEnabled == ThinkTagParserEnabled
            && snapshot.MaxThinkingBudget == MaxThinkingBudget;
    }

    public ModelSnapshot ToSnapshot(short modelId, ModelKey modelKey, DateTime createdAt)
    {
        return new ModelSnapshot
        {
            ModelId = modelId,
            Name = Name,
            DeploymentName = DeploymentName,
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKey.CurrentSnapshotId,
            ModelKeySnapshot = modelKey.CurrentSnapshot,
            InputFreshTokenPrice1M = InputFreshTokenPrice1M,
            OutputTokenPrice1M = OutputTokenPrice1M,
            InputCachedTokenPrice1M = InputCachedTokenPrice1M,
            AllowSearch = AllowSearch,
            AllowVision = AllowVision,
            SupportsVisionLink = SupportsVisionLink,
            AllowStreaming = AllowStreaming,
            AllowCodeExecution = AllowCodeExecution,
            SupportedEfforts = ToSupportedEffortsCsv(SupportedEfforts),
            SupportedFormats = ToCsvOrNull(SupportedFormats),
            OverrideUrl = NullIfWhiteSpace(OverrideUrl),
            CustomHeaders = NullIfWhiteSpace(CustomHeaders),
            CustomBody = NullIfWhiteSpace(CustomBody),
            MinTemperature = MinTemperature,
            MaxTemperature = MaxTemperature,
            ContextWindow = ContextWindow,
            MaxResponseTokens = MaxResponseTokens,
            AllowToolCall = AllowToolCall,
            SupportedImageSizes = SupportedImageSizes.Length > 0 ? string.Join(',', SupportedImageSizes) : null,
            ApiTypeId = (byte)ApiType,
            UseAsyncApi = UseAsyncApi,
            UseMaxCompletionTokens = UseMaxCompletionTokens,
            IsLegacy = IsLegacy,
            ThinkTagParserEnabled = ThinkTagParserEnabled,
            MaxThinkingBudget = MaxThinkingBudget,
            CreatedAt = createdAt,
        };
    }

    private static string? ToSupportedEffortsCsv(IEnumerable<string> supportedEfforts)
    {
        string[] exactValues = [..
            supportedEfforts
                .Where(static value => value is not null)
                .Select(static value => value!)];

        foreach (string effort in exactValues)
        {
            ReasoningEfforts.ThrowIfInvalid(effort);
        }

        return exactValues.Length == 0 ? null : string.Join(',', exactValues);
    }

    private static string? ToCsvOrNull(IEnumerable<string> values)
    {
        string[] cleaned = [.. values.Where(static value => !string.IsNullOrWhiteSpace(value))];
        return cleaned.Length == 0 ? null : string.Join(',', cleaned);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
