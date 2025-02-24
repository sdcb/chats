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
DECLARE @p1 VarChar(1000) = 'InvalidApiHostUrl'
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