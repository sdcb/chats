-- 插入 OpenRouter 到 ModelProvider
INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret])
VALUES (18, 'OpenRouter', 'https://openrouter.ai/api/v1', 'sk-or-v1-***')
GO

-- 插入 OpenRouter 的 ModelReference - openrouter-general
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (1800, 18, 'openrouter-general', null, null, 0, 2, 1, 0, 1, 1, 128000, 8000, null, 0, 0, 'USD')
GO

-- 插入 OpenRouter 的 ModelReference - openrouter-vision
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (1801, 18, 'openrouter-vision', null, null, 0, 2, 1, 1, 1, 1, 128000, 8000, null, 0, 0, 'USD')
GO
