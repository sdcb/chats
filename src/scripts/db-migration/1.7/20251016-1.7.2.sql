PRINT N'* 给ChatConfig表增加CodeExecutionEnabled字段';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'CodeExecutionEnabled' AND Object_ID = Object_ID(N'dbo.ChatConfig'))
BEGIN
    ALTER TABLE dbo.ChatConfig ADD CodeExecutionEnabled BIT NOT NULL CONSTRAINT DF_ChatConfig_CodeExecutionEnabled DEFAULT 0;
    ALTER TABLE dbo.ChatConfig DROP CONSTRAINT DF_ChatConfig_CodeExecutionEnabled;
END

PRINT N'* 调整ChatConfig的ReasoningEffort：所有非0值加1以适配新增的Minimal级别(1/2/3->2/3/4)，仅在当前不存在值4时执行';
IF NOT EXISTS(SELECT * FROM dbo.ChatConfig WHERE ReasoningEffort = 4)
BEGIN
    UPDATE dbo.ChatConfig SET ReasoningEffort = ReasoningEffort + 1 WHERE ReasoningEffort <> 0;
END

PRINT N'* 插入或更新ModelReference表以添加新模型信息';
MERGE INTO [ModelReference] AS target
USING (
    VALUES 
    (133,  1,     'gpt-5-codex', NULL, '2025-09-15', 0, 2, 0, 1, 1, 1, 0, 400000, 128000, 2, 1.25, 10, 'USD'),
    (533,  5,     'gpt-5-codex', NULL, '2025-09-15', 0, 2, 0, 1, 1, 1, 0, 400000, 128000, 2, 1.25, 10, 'USD'),
    (134,  1,       'gpt-5-pro', NULL, '2025-08-07', 0, 2, 0, 1, 1, 1, 1, 400000, 272000, 2, 15,  120, 'USD'),
    (534,  5,       'gpt-5-pro', NULL, '2025-08-07', 1, 1, 0, 1, 1, 1, 1, 400000, 272000, 2, 15,  120, 'USD'),
    (126, 1,      'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 1, 0,  65536, 10,     2,  5,  40,  'USD'),
    (526, 5,      'gpt-image-1', NULL, '2025-04-16', 1, 1, 0, 1, 0, 1, 0,  65536, 10,     2,  5,  40,  'USD'),
    (135, 1, 'gpt-image-1-mini', NULL, '2025-10-06', 1, 1, 0, 1, 0, 1, 0,  65536, 10,     2,  2,   8,  'USD'),
    (535, 5, 'gpt-image-1-mini', NULL, '2025-10-06', 1, 1, 0, 1, 0, 1, 0,  65536, 10,     2,  2,   8,  'USD')
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