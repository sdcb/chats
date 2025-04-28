-- Region Parameters
DECLARE @p0 SmallInt = 301
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 302
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 304
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 305
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 306
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 307
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 308
DECLARE @p1 Date = '2024-01-01'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 303
DECLARE @p1 NVarChar(1000) = 'yi-vision-v2'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0
GO


-- Region Parameters
DECLARE @p0 SmallInt = 303
DECLARE @p1 Int = 4096
-- EndRegion
UPDATE [ModelReference]
SET [MaxResponseTokens] = @p1
WHERE [Id] = @p0
GO

UPDATE "ModelReference"
SET "MaxResponseTokens" = 4096
WHERE "Id" = 300;