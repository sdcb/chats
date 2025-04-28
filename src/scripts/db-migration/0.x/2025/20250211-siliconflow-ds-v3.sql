-- Region Parameters
DECLARE @p0 SmallInt = 1702
DECLARE @p1 Int = 4096
-- EndRegion
UPDATE [ModelReference]
SET [MaxResponseTokens] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1703
DECLARE @p1 Int = 4096
-- EndRegion
UPDATE [ModelReference]
SET [MaxResponseTokens] = @p1
WHERE [Id] = @p0
GO