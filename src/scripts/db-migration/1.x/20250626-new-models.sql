MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (1615, 16, N'deepseek-r1-250528',              'DeepSeek-R1', '2025-05-28', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 4,    12,  'RMB'),
    (1616, 16, N'doubao-seed-1-6-250615',          'Doubao-1.6',  '2025-06-15', 0, 2, 0, 1, 1, 1, 1, 262144, 16384, NULL, 0.6,  8,   'RMB'),
    (1617, 16, N'doubao-seed-1-6-flash-250615',    'Doubao-1.6',  '2025-06-15', 0, 2, 0, 1, 1, 1, 0, 262144, 16384, NULL, 0.15, 1.5, 'RMB'),
    (1302, 13, N'gemini-2.5-pro',   'Gemini 2.5', '2025-05-06', 0,   2, 1, 1, 1, 1, 1, 1048576, 65536, NULL, 1.25, 10,  'USD'),
    (1305, 13, N'gemini-2.5-flash', 'Gemini 2.5', '2025-04-17', 0,   2, 1, 1, 1, 1, 1, 1048576, 65536, NULL, 0.30, 2.5, 'USD'),
    (1501, 15, N'MiniMax-M1',       NULL,         '2025-06-17', 0.8, 1, 0, 0, 1, 1, 1, 1048576, 65536, NULL, 1.2,  16,  'RMB'),
    (124,  1,  'o3',                NULL,         '2025-04-16', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    2,    8,  'USD'),
    (524,  5,  'o3',                NULL,         '2025-04-16', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    2,    8,  'USD'),
    (127,  1,  'o3-pro',            NULL,         '2025-06-10', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    20,   80, 'USD'),
    (527,  5,  'o3-pro',            NULL,         '2025-06-10', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    20,   80, 'USD'),
    (128,  1,  'codex-mini',        NULL,         '2025-05-16', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    1.5,  6,  'USD'),
    (528,  5,  'codex-mini',        NULL,         '2025-05-16', 1,   1, 0, 1, 1, 1, 0, 200000,  100000,2,    1.5,  6,  'USD')
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