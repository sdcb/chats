UPDATE [ModelReference] SET [AllowSearch] = 1 WHERE [Id] = 1301;

INSERT INTO [ModelReference] ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES 
    (1304, 13, 'gemini-2.0-flash-exp',  'gemini', '2025-03-25', 0, 2, 0, 1, 0, 1, 0, 32768, 8000,  NULL, 0,   0, 'USD'),
    (209,  2,  'hunyuan-t1-latest',     NULL,     '2025-04-03', 0, 2, 1, 0, 1, 1, 1, 64000, 64000, NULL, 1,   4, 'RMB'),
    (210,  2,  'hunyuan-turbos-latest', NULL,     '2025-03-13', 0, 2, 1, 0, 1, 1, 0, 32000, 8000, NULL,  0.8, 2, 'RMB');

-- Release Note - 20250317 https://www.volcengine.com/docs/82379/1159177
update ModelReference set MaxTemperature = 2 where ProviderId = 16;