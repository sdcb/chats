INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES 
(121,  1, 'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
(521,  5, 'gpt-4.1',      'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD'),
(522,  5, 'gpt-4.1-mini', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.4, 1.6, 'USD'),
(523,  5, 'gpt-4.1-nano', 'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 0.1, 0.4, 'USD'),
(1223, 12, 'gpt-4.1',     'gpt-4.1', '2025-04-14', 0, 2, 0, 1, 1, 1, 0, 1047576, 32768, 2, 2,   8,   'USD');
