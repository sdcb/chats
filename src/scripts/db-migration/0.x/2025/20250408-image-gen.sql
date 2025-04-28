UPDATE [ModelReference] SET [AllowSearch] = 1 WHERE [Id] = 1301;

INSERT INTO [ModelReference] ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES 
    (1304, 13, 'gemini-2.0-flash-exp',  'gemini', '2025-03-25', 0, 2, 0, 1, 0, 1, 0, 32768,  8000,  NULL, 0,   0, 'USD'),
    (209,  2,  'hunyuan-t1-latest',     NULL,     '2025-04-03', 0, 2, 1, 0, 1, 1, 1, 64000,  64000, NULL, 1,   4, 'RMB'),
    (210,  2,  'hunyuan-turbos-latest', NULL,     '2025-03-13', 0, 2, 1, 0, 1, 1, 0, 32000,  8000, NULL,  0.8, 2, 'RMB'),
    (1104, 11, 'grok-3',                'grok-3', '2025-04-10', 0, 2, 0, 0, 1, 1, 0, 131072, 16384, NULL, 3,   15, 'USD'),
    (1105, 11, 'grok-3-fast',           'grok-3', '2025-04-10', 0, 2, 0, 0, 1, 1, 0, 131072, 16384, NULL, 5,   25, 'USD'),
    (1106, 11, 'grok-3-mini',           'grok-3', '2025-04-10', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 0.3, 0.5,'USD'),
    (1107, 11, 'grok-3-mini-fast',      'grok-3', '2025-04-10', 0, 2, 0, 0, 1, 1, 1, 131072, 16384, NULL, 0.6, 4,  'USD');

-- Release Note - 20250317 https://www.volcengine.com/docs/82379/1159177
update ModelReference set MaxTemperature = 2 where ProviderId = 16;

-- x.ai alias
UPDATE [ModelReference]
SET [Name] = CASE 
                WHEN [Id] = 1102 THEN N'grok-2'
                WHEN [Id] = 1103 THEN N'grok-2-vision'
             END
WHERE [Id] IN (1102, 1103);

