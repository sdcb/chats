using Chats.BE.DB;
using Chats.BE.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record UpdateModelRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("deploymentName")]
    public required string DeploymentName { get; init; }

    [JsonPropertyName("modelKeyId")]
    public required short ModelKeyId { get; init; }

    [JsonPropertyName("inputTokenPrice1M")]
    public required decimal InputTokenPrice1M { get; init; }

    [JsonPropertyName("outputTokenPrice1M")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("allowSearch")]
    public required bool AllowSearch { get; init; }

    [JsonPropertyName("allowVision")]
    public required bool AllowVision { get; init; }

    [JsonPropertyName("allowSystemPrompt")]
    public required bool AllowSystemPrompt { get; init; }

    [JsonPropertyName("allowStreaming")]
    public required bool AllowStreaming { get; init; }

    [JsonPropertyName("allowCodeExecution")]
    public required bool AllowCodeExecution { get; init; }

    [JsonPropertyName("reasoningEffortOptions")]
    public required int[] ReasoningEffortOptions { get; init; }

    [JsonPropertyName("minTemperature")]
    public required decimal MinTemperature { get; init; }

    [JsonPropertyName("maxTemperature")]
    public required decimal MaxTemperature { get; init; }

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
        cm.AllowSystemPrompt = AllowSystemPrompt;
        cm.AllowStreaming = AllowStreaming;
        cm.AllowCodeExecution = AllowCodeExecution;
        cm.ReasoningEffortOptions = string.Join(',', ReasoningEffortOptions);
        cm.MinTemperature = MinTemperature;
        cm.MaxTemperature = MaxTemperature;
        cm.ContextWindow = ContextWindow;
        cm.MaxResponseTokens = MaxResponseTokens;
        cm.AllowToolCall = AllowToolCall;
        cm.SupportedImageSizes = string.Join(',', SupportedImageSizes);
        cm.ApiType = (byte)ApiType;
        cm.UseAsyncApi = UseAsyncApi;
        cm.UseMaxCompletionTokens = UseMaxCompletionTokens;
        cm.IsLegacy = IsLegacy;
        cm.ThinkTagParserEnabled = ThinkTagParserEnabled;
    }
}
