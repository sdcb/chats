--INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
--VALUES 
--(121,  1,  'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
--(122,  1,  'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
--(123,  1,  'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
--(521,  5,  'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
--(522,  5,  'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
--(523,  5,  'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
--(1223, 12, 'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
--(1224, 12, 'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
--(1225, 12, 'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
--(124,  1,  'o3',           NULL,      '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 10,  40,  'USD'),
--(125,  1,  'o4-mini',      NULL,      '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
--(524,  5,  'o3',           NULL,      '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 10,  40,  'USD'),
--(525,  5,  'o4-mini',      NULL,      '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
--(1226, 12, 'o4-mini',      NULL,      '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
--(1609, 16, 'doubao-1-5-thinking-pro-250415',        'Doubao-1.5', '2025-04-15', 1, 1, 0, 0, 1, 1, 1, 131072, 16384, NULL, 4,   16,  'RMB'),
--(1610, 16, 'doubao-1.5-thinking-pro-vision-250415', 'Doubao-1.5', '2025-04-15', 1, 1, 0, 1, 1, 1, 1, 131072, 16384, NULL, 4,   16,  'RMB'),
--(1612, 16, 'doubao-1.5-vision-pro-250328',          'Doubao-1.5', '2025-03-28', 0, 2, 0, 1, 1, 1, 0, 131072, 16384, NULL, 3,   9,   'RMB'),
--(1613, 16, 'doubao-1.5-vision-lite-250315',         'Doubao-1.5', '2025-03-15', 0, 2, 0, 1, 1, 1, 0, 131072, 16384, NULL, 1.5, 4.5, 'RMB'),
--(1614, 16, 'doubao-1.5-ui-tars-250328',             'Doubao-1.5', '2025-03-28', 0, 2, 0, 1, 1, 1, 0, 32768,  4096,  NULL, 3.5, 12,  'RMB');

MERGE INTO [ModelReference] AS target
USING (
    VALUES 
        (121,  1,  'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
        (122,  1,  'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
        (123,  1,  'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
        (521,  5,  'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
        (522,  5,  'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
        (523,  5,  'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
        (1223, 12, 'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
        (1224, 12, 'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
        (1225, 12, 'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
        (124,  1,  'o3',            NULL,     '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 10,  40,  'USD'),
        (125,  1,  'o4-mini',       NULL,     '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
        (524,  5,  'o3',            NULL,     '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 10,  40,  'USD'),
        (525,  5,  'o4-mini',       NULL,     '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
        (1226, 12, 'o4-mini',       NULL,     '2025-04-16', 1, 1, 0, 1, 1, 1, 0, 200000,  100000,2, 1.1, 4.4, 'USD'),
        (1609, 16, 'doubao-1-5-thinking-pro-250415',        'Doubao-1.5', '2025-04-15', 1, 1, 0, 0, 1, 1, 1, 131072, 16384, NULL, 4,   16,  'RMB'),
        (1610, 16, 'doubao-1.5-thinking-pro-vision-250415', 'Doubao-1.5', '2025-04-15', 1, 1, 0, 1, 1, 1, 1, 131072, 16384, NULL, 4,   16,  'RMB'),
        (1612, 16, 'doubao-1.5-vision-pro-250328',          'Doubao-1.5', '2025-03-28', 0, 2, 0, 1, 1, 1, 0, 131072, 16384, NULL, 3,   9,   'RMB'),
        (1613, 16, 'doubao-1.5-vision-lite-250315',         'Doubao-1.5', '2025-03-15', 0, 2, 0, 1, 1, 1, 0, 131072, 16384, NULL, 1.5, 4.5, 'RMB'),
        (1614, 16, 'doubao-1.5-ui-tars-250328',             'Doubao-1.5', '2025-03-28', 0, 2, 0, 1, 1, 1, 0, 32768,  4096,  NULL, 3.5, 12,  'RMB')
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

UPDATE [ModelReference]
SET [DisplayName] = 'grok-3'
WHERE [Id] IN (1104, 1105, 1106, 1107);