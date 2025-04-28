INSERT INTO [ModelProvider] ([Id], [Name], [InitialHost], [InitialSecret], [RequireDeploymentName])
VALUES (18, 'OpenRouter', 'https://openrouter.ai/api/v1', 'sk-or-v1-***', 1);

INSERT INTO [ModelReference] ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (1800, 18, 'openrouter-general', NULL, NULL, 0, 2, 1, 0, 1, 1, 0, 128000, 8000, NULL, 0, 0, 'USD');

INSERT INTO [ModelReference] ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (1801, 18, 'openrouter-vision', NULL, NULL, 0, 2, 1, 1, 1, 1, 0, 128000, 8000, NULL, 0, 0, 'USD');
