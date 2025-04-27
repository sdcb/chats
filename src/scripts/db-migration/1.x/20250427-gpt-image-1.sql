--DELETE FROM ModelReference WHERE Id IN(126, 526);
INSERT INTO [ModelReference]
    ([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
     [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
     [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES
    (126, 1, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 32768, 2, 5, 40, 'USD'),
    (526, 5, N'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 0, 0, 65536, 32768, 2, 5, 40, 'USD');