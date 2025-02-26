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

-- Region Parameters
DECLARE @p0 SmallInt = 601
DECLARE @p1 NVarChar(1000) = 'ernie-4.0-8k'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 602
DECLARE @p1 NVarChar(1000) = 'ernie-3.5-8k'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 607
DECLARE @p1 NVarChar(1000) = 'ernie-speed-8k'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 615
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'deepseek-v3'
DECLARE @p3 NVarChar(1000) = 'DeepSeek'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(5,2) = 0.6
DECLARE @p6 Decimal(5,2) = 0.6
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = null
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 615
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'deepseek-v3'
DECLARE @p3 NVarChar(1000) = 'DeepSeek'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(8,5) = 0.8
DECLARE @p16 Decimal(8,5) = 1.6
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 616
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'deepseek-r1'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 2
DECLARE @p16 Decimal(6,5) = 8
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 615
DECLARE @p1 TinyInt = 0
-- EndRegion
UPDATE [ModelReference]
SET [ReasoningResponseKindId] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 617
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Qwen-32B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(9,5) = 0.15
DECLARE @p16 Decimal(6,5) = 6
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 600
DECLARE @p1 Decimal(7,5) = 20
DECLARE @p2 Decimal(7,5) = 60
-- EndRegion
UPDATE [ModelReference]
SET [InputTokenPrice1M] = @p1, [OutputTokenPrice1M] = @p2
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 601
DECLARE @p1 Decimal(7,5) = 30
DECLARE @p2 Decimal(7,5) = 90
-- EndRegion
UPDATE [ModelReference]
SET [InputTokenPrice1M] = @p1, [OutputTokenPrice1M] = @p2
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 600
DECLARE @p1 NVarChar(1000) = 'ernie-4.0-turbo-128k'
DECLARE @p2 NVarChar(1000) = 'ERNIE-4.0-Turbo'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1, [DisplayName] = @p2
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 601
DECLARE @p1 NVarChar(1000) = 'ERNIE-4.0'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 602
DECLARE @p1 NVarChar(1000) = 'ERNIE-3.5'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 603
DECLARE @p1 NVarChar(1000) = 'ERNIE-3.5'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 604
DECLARE @p1 NVarChar(1000) = 'ERNIE-Speed-Pro'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 605
DECLARE @p1 NVarChar(1000) = 'ERNIE-Novel'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 606
DECLARE @p1 NVarChar(1000) = 'ERNIE-Speed'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 607
DECLARE @p1 NVarChar(1000) = 'ERNIE-Speed'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 609
DECLARE @p1 NVarChar(1000) = 'ERNIE-Lite'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 611
DECLARE @p1 NVarChar(1000) = 'ERNIE-Tiny'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 614
DECLARE @p1 NVarChar(1000) = 'ERNIE-Lite-Pro'
-- EndRegion
UPDATE [ModelReference]
SET [DisplayName] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 618
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Qwen-14B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = null
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 618
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Qwen-14B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(8,5) = 0.6
DECLARE @p16 Decimal(8,5) = 2.4
DECLARE @p17 Char(3) = null
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 618
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Qwen-14B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(8,5) = 0.6
DECLARE @p16 Decimal(8,5) = 2.4
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 617
DECLARE @p1 Decimal(8,5) = 1.5
-- EndRegion
UPDATE [ModelReference]
SET [InputTokenPrice1M] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 619
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Qwen-7B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 620
DECLARE @p1 SmallInt = 6
DECLARE @p2 NVarChar(1000) = 'DeepSeek-R1-Distill-Llama-70B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 2
DECLARE @p16 Decimal(6,5) = 8
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
