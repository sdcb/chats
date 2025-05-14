MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (738, 7,   N'qwen3-235b-a22b', 'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 4,   12,  'RMB'),
    (739, 7,   N'qwen3-32b',       'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 2,   8,   'RMB'),
    (740, 7,   N'qwen3-30b-a3b',   'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 1.5, 6,   'RMB'),
    (741, 7,   N'qwen3-14b',       'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192,  NULL, 1,   4,   'RMB'),
    (742, 7,   N'qwen3-8b',        'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192,  NULL, 0.5, 2,   'RMB'),
    (743, 7,   N'qwen3-4b',        'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192,  NULL, 0.3, 1.2, 'RMB'),
    (744, 7,   N'qwen3-1.7b',      'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0.3, 1.2, 'RMB'),
    (745, 7,   N'qwen3-0.6b',      'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0.3, 1.2, 'RMB'),
    (1711, 17,   N'Qwen/Qwen3-235B-A22B', 'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192, NULL, 2.5, 10,  'RMB'),
    (1712, 17,   N'Qwen/Qwen3-32B',       'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192, NULL, 1,   4,   'RMB'),
    (1713, 17,   N'Qwen/Qwen3-30B-A3B',   'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192, NULL, 0.7, 2.8, 'RMB'),
    (1714, 17,   N'Qwen/Qwen3-14B',       'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192,  NULL, 0.5, 2,   'RMB'),
    (1715, 17,   N'Qwen/Qwen3-8B',        'qwen3', '2025-04-28', 0, 2, 0, 0, 1, 1, 1, 131072, 8192,  NULL, 0,   0,   'RMB')
) AS source (
    [Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
    [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
    [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
)
ON target.[Id] = source.[Id]
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