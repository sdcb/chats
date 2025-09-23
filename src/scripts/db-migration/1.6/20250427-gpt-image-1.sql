--INSERT INTO [ModelReference]
--    ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
--     [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
--     [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
--VALUES
--    (126, 1, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 10, 2, 5, 40, 'USD'),
--    (526, 5, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 10, 2, 5, 40, 'USD'),
--    (622, 6, N'ernie-x1-turbo-32k',     'ERNIE-X1',  '2025-04-27', 1, 1, 1, 0, 1, 1, 1, 32768, 12288, NULL, 1.0, 4.0, 'RMB'),
--    (623, 6, N'ernie-4.5-turbo-vl-32k', 'ERNIE-4.5', '2025-04-27', 0, 1, 1, 1, 1, 1, 0, 32768, 12288, NULL, 3.0, 9.0, 'RMB'),
--    (624, 6, N'ernie-4.5-turbo-128k',   'ERNIE-4.5', '2025-04-27', 0, 1, 1, 0, 1, 1, 0, 131072,12288, NULL, 0.8, 3.2, 'RMB'),
--    (625, 6, N'ernie-4.5-turbo-32k',    'ERNIE-4.5', '2025-04-27', 0, 1, 1, 0, 1, 1, 0, 32768, 12288, NULL, 0.8, 3.2, 'RMB');

MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (126, 1, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 10, 2, 5, 40, 'USD'),
    (526, 5, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 10, 2, 5, 40, 'USD'),
    (622, 6, N'ernie-x1-turbo-32k',     'ERNIE-X1',  '2025-04-27', 1, 1, 1, 0, 1, 1, 1, 32768, 12288, NULL, 1.0, 4.0, 'RMB'),
    (623, 6, N'ernie-4.5-turbo-vl-32k', 'ERNIE-4.5', '2025-04-27', 0, 1, 1, 1, 1, 1, 0, 32768, 12288, NULL, 3.0, 9.0, 'RMB'),
    (624, 6, N'ernie-4.5-turbo-128k',   'ERNIE-4.5', '2025-04-27', 0, 1, 1, 0, 1, 1, 0, 131072,12288, NULL, 0.8, 3.2, 'RMB'),
    (625, 6, N'ernie-4.5-turbo-32k',    'ERNIE-4.5', '2025-04-27', 0, 1, 1, 0, 1, 1, 0, 32768, 12288, NULL, 0.8, 3.2, 'RMB')
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