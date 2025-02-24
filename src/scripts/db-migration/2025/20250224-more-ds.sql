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