-- Region Parameters
DECLARE @p0 SmallInt = 1402
DECLARE @p1 NVarChar(1000) = 'deepseek-r1-think-tag'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0

--For SQLite:
--UPDATE `ModelReference`
--SET `Name` = 'deepseek-r1-think-tag'
--WHERE `Id` = 1402