-- Region Parameters
DECLARE @p0 SmallInt = 2
DECLARE @p1 VarChar(1000) = 'https://api.hunyuan.cloud.tencent.com/v1'
DECLARE @p2 VarChar(1000) = 'sk-'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1, [InitialSecret] = @p2
WHERE [Id] = @p0
GO

update ModelKey set host = 'https://api.hunyuan.cloud.tencent.com/v1' where ModelProviderId = 2
GO

-- Region Parameters
DECLARE @p0 TinyInt = 107
DECLARE @p1 VarChar(1000) = 'InternalConfigIssue'
-- EndRegion
INSERT INTO [FinishReason]([Id], [Name])
VALUES (@p0, @p1)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 6
DECLARE @p1 VarChar(1000) = 'https://qianfan.baidubce.com/v2'
DECLARE @p2 VarChar(1000) = '{"appId": "app-***", "apiKey":"bce-v3/***"}'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1, [InitialSecret] = @p2
WHERE [Id] = @p0
GO

update ModelKey set host = 'https://qianfan.baidubce.com/v2' where ModelProviderId = 6
GO

-- Region Parameters
DECLARE @p0 SmallInt = 7
DECLARE @p1 VarChar(1000) = 'https://dashscope.aliyuncs.com/compatible-mode/v1'
DECLARE @p2 VarChar(1000) = 'sk-***'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1, [InitialSecret] = @p2
WHERE [Id] = @p0

update ModelKey set host = 'https://dashscope.aliyuncs.com/compatible-mode/v1' where ModelProviderId = 7
GO

-- Region Parameters
DECLARE @p0 SmallInt = 13
DECLARE @p1 VarChar(1000) = 'https://generativelanguage.googleapis.com/v1beta/openai/'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 3
DECLARE @p1 VarChar(1000) = 'https://api.lingyiwanwu.com/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 4
DECLARE @p1 VarChar(1000) = 'https://api.moonshot.cn/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 8
DECLARE @p1 VarChar(1000) = 'https://spark-api-open.xf-yun.com/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 9
DECLARE @p1 VarChar(1000) = 'https://open.bigmodel.cn/api/paas/v4/'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 10
DECLARE @p1 VarChar(1000) = 'https://api.deepseek.com/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 11
DECLARE @p1 VarChar(1000) = 'https://api.x.ai/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 12
DECLARE @p1 VarChar(1000) = 'https://models.inference.ai.azure.com'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 15
DECLARE @p1 VarChar(1000) = 'https://api.minimax.chat/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 16
DECLARE @p1 VarChar(1000) = 'https://ark.cn-beijing.volces.com/api/v3/'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 17
DECLARE @p1 VarChar(1000) = 'https://api.siliconflow.cn/v1'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1
WHERE [Id] = @p0
GO

UPDATE [ModelReference]
SET 
    [Name] = CASE 
        WHEN [Id] = 600 THEN 'ernie-4.0-turbo-128k'
        WHEN [Id] = 601 THEN 'ernie-4.0-8k'
        WHEN [Id] = 602 THEN 'ernie-3.5-8k'
        WHEN [Id] = 607 THEN 'ernie-speed-8k'
        ELSE [Name]
    END,
    [DisplayName] = CASE 
        WHEN [Id] = 600 THEN 'ERNIE-4.0-Turbo'
        WHEN [Id] = 601 THEN 'ERNIE-4.0'
        WHEN [Id] = 602 THEN 'ERNIE-3.5'
        WHEN [Id] = 603 THEN 'ERNIE-3.5'
        WHEN [Id] = 604 THEN 'ERNIE-Speed-Pro'
        WHEN [Id] = 605 THEN 'ERNIE-Novel'
        WHEN [Id] = 606 THEN 'ERNIE-Speed'
        WHEN [Id] = 607 THEN 'ERNIE-Speed'
        WHEN [Id] = 609 THEN 'ERNIE-Lite'
        WHEN [Id] = 611 THEN 'ERNIE-Tiny'
        WHEN [Id] = 614 THEN 'ERNIE-Lite-Pro'
        ELSE [DisplayName]
    END,
    [InputTokenPrice1M] = CASE 
        WHEN [Id] = 600 THEN 20
        WHEN [Id] = 601 THEN 30
        ELSE [InputTokenPrice1M]
    END,
    [OutputTokenPrice1M] = CASE 
        WHEN [Id] = 600 THEN 60
        WHEN [Id] = 601 THEN 90
        ELSE [OutputTokenPrice1M]
    END
WHERE [Id] BETWEEN 600 AND 614;

-- 合并插入 Id>=615 的操作
INSERT INTO [ModelReference](
    [Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
    [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
    [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
)
VALUES 
    (615, 6, 'deepseek-v3', 'DeepSeek-V3', NULL, 0, 2, 0, 0, 1, 1, 0, 64000, 8192, NULL, 0.8, 1.6, 'RMB'),
    (616, 6, 'deepseek-r1', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 64000, 8192, NULL, 2, 8, 'RMB'),
    (617, 6, 'deepseek-r1-distill-qwen-32b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 64000, 8192, NULL, 1.5, 6, 'RMB'),
    (618, 6, 'deepseek-r1-distill-qwen-14b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 64000, 8192, NULL, 0.6, 2.4, 'RMB'),
    (619, 6, 'deepseek-r1-distill-qwen-7b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 64000, 8192, NULL, 0, 0, 'RMB'),
    (620, 6, 'deepseek-r1-distill-llama-70b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 64000, 8192, NULL, 2, 8, 'RMB');
GO

-- Region Parameters
DECLARE @p0 SmallInt = 204
DECLARE @p1 Bit = 0
-- EndRegion
UPDATE [ModelReference]
SET [AllowSearch] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1303
DECLARE @p1 NVarChar(1000) = 'gemini-2.0-flash-lite'
DECLARE @p2 Date = '2025-02-26'
DECLARE @p3 Decimal(10,5) = 0.075
DECLARE @p4 Decimal(8,5) = 0.3
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1, [PublishDate] = @p2, [InputTokenPrice1M] = @p3, [OutputTokenPrice1M] = @p4
WHERE [Id] = @p0
GO

INSERT INTO[ModelReference](
	[Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature],
	[AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId],
	[ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
)
VALUES
	(727, 7, 'deepseek-v3', 'DeepSeek-V3', NULL, 0, 2, 0, 0, 1, 1, 0, 65792, 8192, NULL, 2, 8, 'RMB'),
    (728, 7, 'deepseek-r1', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 65792, 8192, NULL, 4, 16, 'RMB'),
    (729, 7, 'deepseek-r1-distill-qwen-32b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 2, 6, 'RMB'),
    (730, 7, 'deepseek-r1-distill-qwen-14b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 1, 3, 'RMB'),
    (731, 7, 'deepseek-r1-distill-qwen-7b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0.5, 1, 'RMB'),
	(732, 7, 'deepseek-r1-distill-qwen-1.5b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0, 0, 'RMB'),
    (733, 7, 'deepseek-r1-distill-llama-70b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0, 0, 'RMB'),
	(734, 7, 'deepseek-r1-distill-llama-8b', 'DeepSeek-R1', NULL, 0, 2, 0, 0, 1, 1, 1, 32768, 16384, NULL, 0, 0, 'RMB');
GO

INSERT INTO [ModelReference](
    [Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], 
    [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], 
    [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode]
)
VALUES 
    -- 文本模型
    (1600, 16, 'doubao-1.5-pro-32k-250115', 'Doubao-1.5-Pro', '2025/01/15', 0, 1, 0, 0, 1, 1, 0, 32000, 12288, NULL, 0.8, 2.0, 'RMB'),
    (1601, 16, 'doubao-1.5-pro-256k-250115', 'Doubao-1.5-Pro', '2025/01/15', 0, 1, 0, 0, 1, 1, 0, 256000, 12288, NULL, 5, 9, 'RMB'),
    (1602, 16, 'doubao-1.5-lite-32k-250115', 'Doubao-1.5-Lite', '2025/01/15', 0, 1, 0, 0, 1, 1, 0, 32000, 12288, NULL, 0.3, 0.6, 'RMB'),
    (1603, 16, 'deepseek-r1-250120', 'DeepSeek-R1', '2025/01/20', 0, 1, 0, 0, 1, 1, 1, 64000, 16384, NULL, 4, 16, 'RMB'),
    (1604, 16, 'deepseek-r1-distill-qwen-32b-250120', 'DeepSeek-R1', '2025/01/20', 0, 1, 0, 0, 1, 1, 1, 32000, 8192, NULL, 1.5, 6, 'RMB'),
    (1605, 16, 'deepseek-r1-distill-qwen-7b-250120', 'DeepSeek-R1', '2025/01/20', 0, 1, 0, 0, 1, 1, 1, 32000, 8192, NULL, 0.6, 2.4, 'RMB'),
    (1606, 16, 'deepseek-v3-241226', 'DeepSeek-V3', '2025/01/20', 0, 1, 0, 0, 1, 1, 0, 64000, 16384, NULL, 2, 8, 'RMB'),

    -- 视觉模型
    (1607, 16, 'doubao-1.5-vision-pro-32k-250115', 'Doubao-1.5-Vision-Pro', '2025/01/15', 0, 1, 0, 1, 1, 1, 0, 32000, 12288, NULL, 3, 9, 'RMB');
GO
