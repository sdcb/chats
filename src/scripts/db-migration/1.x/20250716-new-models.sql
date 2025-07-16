MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (1108, 11, N'grok-4', NULL, '2025-07-09', 0, 2, 1, 1, 1, 1, 1, 262144, 16384, NULL, 3, 15, 'USD')
) AS source (
    [Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
    [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
    [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
)
ON target.[Id] = source.[Id]
WHEN MATCHED THEN
    UPDATE SET
        [ProviderId] = source.[ProviderId],
        [Name] = source.[Name],
        [DisplayName] = source.[DisplayName],
        [PublishDate] = source.[PublishDate],
        [MinTemperature] = source.[MinTemperature],
        [MaxTemperature] = source.[MaxTemperature],
        [AllowSearch] = source.[AllowSearch],
        [AllowVision] = source.[AllowVision],
        [AllowSystemPrompt] = source.[AllowSystemPrompt],
        [AllowStreaming] = source.[AllowStreaming],
        [ReasoningResponseKindId] = source.[ReasoningResponseKindId],
        [ContextWindow] = source.[ContextWindow],
        [MaxResponseTokens] = source.[MaxResponseTokens],
        [TokenizerId] = source.[TokenizerId],
        [InputTokenPrice1M] = source.[InputTokenPrice1M],
        [OutputTokenPrice1M] = source.[OutputTokenPrice1M],
        [CurrencyCode] = source.[CurrencyCode]
WHEN NOT MATCHED THEN
    INSERT (
        [Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
        [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
        [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
    )
    VALUES (
        source.[Id], source.[ProviderId], source.[Name], source.[DisplayName], source.[PublishDate], 
        source.[MinTemperature], source.[MaxTemperature], source.[AllowSearch], source.[AllowVision], 
        source.[AllowSystemPrompt], source.[AllowStreaming], source.[ReasoningResponseKindId], 
        source.[ContextWindow], source.[MaxResponseTokens], source.[TokenizerId], source.[InputTokenPrice1M], 
        source.[OutputTokenPrice1M], source.[CurrencyCode]
    );