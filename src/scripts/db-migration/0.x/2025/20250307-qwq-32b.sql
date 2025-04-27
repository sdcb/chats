-- 添加阿里通义千问qwq-32b-preview和qwq-plus模型
-- 添加siliconflow的Qwen/QwQ-32B模型

INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES 
(735, 7, 'qwq-32b',  NULL, '2025-03-07', 0.00, 1.99, 0, 0, 1, 1, 1, 131072, 8192, NULL, 0.00000, 0.00000, 'RMB'),
(736, 7, 'qwq-plus', NULL, '2025-03-07', 0.00, 1.99, 0, 0, 1, 1, 1, 131072, 8192, NULL, 0.00000, 0.00000, 'RMB'),
(1710, 17, 'Qwen/QwQ-32B', NULL, '2025-03-07', 0.00, 2.00, 0, 0, 1, 1, 1, 32768, 8192, NULL, 1.00000, 4.00000, 'RMB')
GO

-- 更新qwq-32b-preview的PublishDate为2024/11/27
UPDATE [ModelReference]
SET [PublishDate] = '2024-11-27'
WHERE [Id] = 735
GO