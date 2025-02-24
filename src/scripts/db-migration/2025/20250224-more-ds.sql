-- Region Parameters
DECLARE @p0 SmallInt = 2
DECLARE @p1 VarChar(1000) = 'https://api.hunyuan.cloud.tencent.com/v1'
DECLARE @p2 VarChar(1000) = 'sk-'
-- EndRegion
UPDATE [ModelProvider]
SET [InitialHost] = @p1, [InitialSecret] = @p2
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 TinyInt = 107
DECLARE @p1 VarChar(1000) = 'InvalidApiHostUrl'
-- EndRegion
INSERT INTO [FinishReason]([Id], [Name])
VALUES (@p0, @p1)
GO