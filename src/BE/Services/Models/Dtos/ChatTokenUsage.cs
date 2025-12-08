namespace Chats.BE.Services.Models.Dtos;

using System;

public record ChatTokenUsage
{
    public required int InputTokens { get; init; }

    public required int OutputTokens { get; init; }

    public int ReasoningTokens { get; init; }

    public int CacheTokens { get; init; }

    public int CacheCreationTokens { get; init; }

    public int InputFreshTokens => Math.Max(0, InputTokens - CacheTokens);

    public static ChatTokenUsage Zero { get; } = new ChatTokenUsage
    {
        InputTokens = 0,
        OutputTokens = 0,
        ReasoningTokens = 0,
        CacheTokens = 0,
        CacheCreationTokens = 0,
    };
}
