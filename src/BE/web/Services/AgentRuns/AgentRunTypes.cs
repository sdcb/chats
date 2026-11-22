using System.Text.Json.Serialization;

namespace Chats.BE.Services.AgentRuns;

public enum ChatRunKind : byte
{
    General = 1,
    RegenerateOne = 2,
    RegenerateAll = 3,
    Continue = 4,
}

public enum ChatRunStatus : byte
{
    Running = 1,
    Completed = 2,
    Stopped = 3,
    Failed = 4,
}

public enum ChatRunSteeringStatus : byte
{
    Queued = 1,
    Injected = 2,
    Discarded = 3,
}

public static class ChatRunModes
{
    public const string Normal = "normal";
    public const string Plan = "plan";

    public static string Normalize(string? mode) => string.Equals(mode, Plan, StringComparison.OrdinalIgnoreCase) ? Plan : Normal;
}

public sealed record SteeringDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("status")]
    public required ChatRunSteeringStatus Status { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Persisted in <c>ChatRun.Metadata</c> so that any instance can rebuild the run's placeholders for resuming clients.
/// </summary>
public sealed record ChatRunMetadata
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = ChatRunModes.Normal;

    [JsonPropertyName("userTurnId")]
    public long? UserTurnId { get; init; }

    [JsonPropertyName("parentAssistantTurnId")]
    public long? ParentAssistantTurnId { get; init; }

    [JsonPropertyName("parentUserTurnId")]
    public long? ParentUserTurnId { get; init; }

    [JsonPropertyName("assistantTurnId")]
    public long? AssistantTurnId { get; init; }

    [JsonPropertyName("spans")]
    public ChatRunSpanInfo[] Spans { get; init; } = [];
}

public sealed record ChatRunSpanInfo
{
    [JsonPropertyName("spanId")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    [JsonPropertyName("modelName")]
    public required string ModelName { get; init; }
}
