using System.Text.Json.Serialization;

namespace Chats.BE.Services.TitleSummary;

[JsonConverter(typeof(JsonStringEnumConverter<TitleSummaryModelMode>))]
public enum TitleSummaryModelMode
{
    [JsonStringEnumMemberName("truncate")]
    Truncate,

    [JsonStringEnumMemberName("current")]
    Current,

    [JsonStringEnumMemberName("specified")]
    Specified,
}

public sealed record TitleSummaryConfig
{
    [JsonPropertyName("modelMode")]
    public required TitleSummaryModelMode ModelMode { get; init; }

    [JsonPropertyName("modelId")]
    public short? ModelId { get; init; }

    [JsonPropertyName("promptTemplate")]
    public string? PromptTemplate { get; init; }
}

public sealed record ResolvedTitleSummaryConfig
{
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("modelMode")]
    public required TitleSummaryModelMode ModelMode { get; init; }

    [JsonPropertyName("modelId")]
    public short? ModelId { get; init; }

    [JsonPropertyName("promptTemplate")]
    public required string PromptTemplate { get; init; }
}
