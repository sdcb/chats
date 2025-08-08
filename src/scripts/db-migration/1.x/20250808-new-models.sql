MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (129,  1,  'gpt-5',      'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
    (130,  1,  'gpt-5-mini', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.25, 2.0, 'USD'),
    (131,  1,  'gpt-5-nano', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.05, 0.4, 'USD'),
    (132,  1,  'gpt-5-chat', 'gpt-5', '2025-08-08', 0, 2, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
    (529,  5,  'gpt-5',      'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
    (530,  5,  'gpt-5-mini', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.25, 2.0, 'USD'),
    (531,  5,  'gpt-5-nano', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.05, 0.4, 'USD'),
    (532,  5,  'gpt-5-chat', 'gpt-5', '2025-08-08', 0, 2, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
	(1227, 12, 'gpt-5',      'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
    (1228, 12, 'gpt-5-mini', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.25, 2.0, 'USD'),
    (1229, 12, 'gpt-5-nano', 'gpt-5', '2025-08-08', 1, 1, 0, 1, 1, 1, 1, 400000, 128000, 2, 0.05, 0.4, 'USD'),
    (1230, 12, 'gpt-5-chat', 'gpt-5', '2025-08-08', 0, 2, 0, 1, 1, 1, 1, 400000, 128000, 2, 1.25, 10,  'USD'),
	(909,  9,  'glm-z1-air',     'glm-z1', '2025-04-14', 0, 1, 1, 0, 1, 1, 2, 128000, 32000, NULL, 0.5, 0.5, 'RMB'),
    (910,  9,  'glm-z1-airx',    'glm-z1', '2025-04-14', 0, 1, 1, 0, 1, 1, 2, 32000,  30000, NULL, 5,   5,   'RMB'),
    (911,  9,  'glm-z1-flash',   'glm-z1', '2025-04-14', 0, 1, 1, 0, 1, 1, 2, 128000, 32000, NULL, 0,   0,   'RMB'),
    (912,  9,  'glm-z1-flashx',  'glm-z1', '2025-04-14', 0, 1, 1, 0, 1, 1, 2, 128000, 32000, NULL, 0.1, 0.1, 'RMB'),
    (913,  9,  'glm-4.5',       'glm-4.5', '2025-07-01', 0, 1, 1, 0, 1, 1, 1, 128000, 96000, NULL, 2,   8,   'RMB'),
    (914,  9,  'glm-4.5-x',     'glm-4.5', '2025-07-01', 0, 1, 1, 0, 1, 1, 1, 128000, 96000, NULL, 8,   32,  'RMB'),
    (915,  9,  'glm-4.5-air',   'glm-4.5', '2025-07-01', 0, 1, 1, 0, 1, 1, 1, 128000, 96000, NULL, 0.6, 4,   'RMB'),
    (916,  9,  'glm-4.5-airx',  'glm-4.5', '2025-07-01', 0, 1, 1, 0, 1, 1, 1, 128000, 96000, NULL, 4,   16,  'RMB'),
    (917,  9,  'glm-4.5-flash', 'glm-4.5', '2025-07-01', 0, 1, 1, 0, 1, 1, 1, 128000, 96000, NULL, 0,   0,   'RMB'),
    (746,  7,  'qwen3-coder-plus',        'qwen3-coder', '2025-07-22', 0, 1.99, 0, 0, 1, 1, 0, 1000000, 65536, NULL, 5,    20,  'RMB'),
    (747,  7,  'qwen3-coder-flash',       'qwen3-coder', '2025-07-22', 0, 1.99, 0, 0, 1, 1, 0, 1000000, 65536, NULL, 2.5,  10,  'RMB'),
    (748,  7,  'qwen3-235b-a22b-thinking-2507', 'qwen3', '2025-07-28', 0, 1.99, 0, 0, 1, 1, 1, 131072,  32768, NULL, 2,    20,  'RMB'),
    (749,  7,  'qwen3-235b-a22b-instruct-2507', 'qwen3', '2025-07-28', 0, 1.99, 0, 0, 1, 1, 0, 131072,  32768, NULL, 2,    8,   'RMB'),
	(750,  7,  'qwen3-30b-a3b-thinking-2507',   'qwen3', '2025-07-28', 0, 1.99, 0, 0, 1, 1, 1, 131072,  32768, NULL, 0.75, 7.5, 'RMB'),
    (751,  7,  'qwen3-30b-a3b-instruct-2507',   'qwen3', '2025-07-28', 0, 1.99, 0, 0, 1, 1, 0, 131072,  32768, NULL, 0.75, 3,   'RMB'),
	(752,  7,  'glm-4.5',                     'glm-4.5', '2025-07-01', 0, 1,    0, 0, 1, 1, 1, 128000,  96000, NULL, 4,    16,  'RMB'),
	(753,  7,  'glm-4.5-air',                 'glm-4.5', '2025-07-01', 0, 1,    0, 0, 1, 1, 1, 128000,  96000, NULL, 1.2,  8,   'RMB'),
	(1718, 17, 'zai-org/GLM-4.5',                     'GLM-4.5', '2025-07-01', 0, 1, 0, 0, 1, 1, 1, 128000,  96000, NULL, 3.5, 14,  'RMB'),
	(1719, 17, 'zai-org/GLM-4.5-Air',                 'GLM-4.5', '2025-07-01', 0, 1, 0, 0, 1, 1, 1, 128000,  96000, NULL, 1,   6,   'RMB'),
	(1720, 17, 'stepfun-ai/step3',                    'Step3',   '2025-07-31', 0, 2, 0, 0, 1, 1, 1, 65536,  65535,  NULL, 4,   10,  'RMB'),
	(1721, 17, 'Qwen/Qwen3-Coder-30B-A3B-Instruct',   'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 262143, NULL, 0.7, 2.8, 'RMB'),
	(1722, 17, 'Qwen/Qwen3-Coder-480B-A35B-Instruct', 'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 262143, NULL, 8,   16,  'RMB'),
	(1723, 17, 'Qwen/Qwen3-30B-A3B-Thinking-2507',    'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 131072, NULL, 0.7, 2.8, 'RMB'),
	(1724, 17, 'Qwen/Qwen3-30B-A3B-Instruct-2507',    'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 262143, NULL, 0.7, 2.8, 'RMB'),
	(1725, 17, 'Qwen/Qwen3-235B-A22B-Thinking-2507',  'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 262143, NULL, 2.5, 10,  'RMB'),
	(1726, 17, 'Qwen/Qwen3-235B-A22B-Instruct-2507',  'Qwen3',   '2025-07-21', 0, 2, 0, 0, 1, 1, 1, 262144, 262143, NULL, 2.5, 10,  'RMB')
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