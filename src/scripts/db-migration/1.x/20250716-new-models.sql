MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (1108, 11, 'grok-4',                          NULL,   '2025-07-09', 0, 2, 1, 1, 1, 1, 1, 262144, 16384, NULL, 3,  15, 'USD'),
    (400,  4,  'moonshot-v1-8k',                  'moonshot', null,     0, 1, 0, 0, 1, 1, 0, 8192,   8192,  NULL, 2,  10, 'RMB'),
    (401,  4,  'moonshot-v1-32k',                 'moonshot', null,     0, 1, 0, 0, 1, 1, 0, 32768,  16384, NULL, 5,  20, 'RMB'),
    (402,  4,  'moonshot-v1-128k',                'moonshot', null,     0, 1, 0, 0, 1, 1, 0, 131072, 16384, NULL, 10, 30, 'RMB'),
    (403,  4,  'kimi-latest',                     'kimi',     null,     0, 1, 0, 1, 1, 1, 0, 131072, 16384, NULL, 2,  10, 'RMB'),
    (404,  4,  'kimi-k2-0711-preview',            'kimi', '2025-07-11', 0, 1, 0, 0, 1, 1, 0, 131072, 16384, NULL, 4,  16, 'RMB'),
    (1716, 17, 'moonshotai/Kimi-K2-Instruct',     'kimi', '2025-07-11', 0, 1, 0, 0, 1, 1, 0, 131072, 16384, NULL, 4,  16, 'RMB'),
    (1717, 17, 'Pro/moonshotai/Kimi-K2-Instruct', 'kimi', '2025-07-11', 0, 1, 0, 0, 1, 1, 0, 131072, 16384, NULL, 4,  16, 'RMB')
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