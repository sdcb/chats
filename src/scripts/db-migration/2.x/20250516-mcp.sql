-- 将UserModelUsage表的UserModelId字段改成UserId和ModelId，ModelId不再引用UserModel表——而引用Model表，UserId引用User表

-- 1. 首先添加新字段
ALTER TABLE [dbo].[UserModelUsage] ADD
    [UserId] [int] NULL,
    [ModelId] [smallint] NULL
GO

-- 2. 通过UserModel表更新新字段的值
UPDATE u
SET u.UserId = m.UserId,
    u.ModelId = m.ModelId
FROM [dbo].[UserModelUsage] u
INNER JOIN [dbo].[UserModel] m ON u.UserModelId = m.Id
GO

-- 3. 删除原有的外键约束/原有的索引
ALTER TABLE [dbo].[UserModelUsage] DROP CONSTRAINT [FK_ModelUsage_UserModel2]
DROP INDEX [IX_ModelUsage_UserModelId] ON [dbo].[UserModelUsage]
GO

-- 4. 添加新的外键约束
ALTER TABLE [dbo].[UserModelUsage] 
ADD CONSTRAINT [FK_UserModelUsage_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO

ALTER TABLE [dbo].[UserModelUsage] 
ADD CONSTRAINT [FK_UserModelUsage_Model] FOREIGN KEY([ModelId])
REFERENCES [dbo].[Model] ([Id])
GO

-- 5. 将新字段设置为非空
ALTER TABLE [dbo].[UserModelUsage] ALTER COLUMN [UserId] [int] NOT NULL
ALTER TABLE [dbo].[UserModelUsage] ALTER COLUMN [ModelId] [smallint] NOT NULL
GO

-- 6. 删除旧的UserModelId字段
ALTER TABLE [dbo].[UserModelUsage] DROP COLUMN [UserModelId]
GO

-- 7. 创建新的索引
CREATE NONCLUSTERED INDEX [IX_UserModelUsage_UserId] ON [dbo].[UserModelUsage]
(
    [UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_UserModelUsage_ModelId] ON [dbo].[UserModelUsage]
(
    [ModelId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO






-- 将UsageTransaction表的UserModelId字段改成ModelId（这个表已经有一个CreateUserId了），ModelId不再引用UserModel表——而引用Model表
-- 1. 添加新字段
ALTER TABLE [dbo].[UsageTransaction] ADD
    [ModelId] [smallint] NULL
GO

-- 2. 通过UserModel表更新新字段的值
UPDATE t
SET t.ModelId = m.ModelId
FROM [dbo].[UsageTransaction] t
INNER JOIN [dbo].[UserModel] m ON t.UserModelId = m.Id
GO

-- 3. 删除原有的外键约束和索引
ALTER TABLE [dbo].[UsageTransaction] DROP CONSTRAINT [FK_UsageTransaction_UserModel]
DROP INDEX [IX_UsageTransaction_UserModelId] ON [dbo].[UsageTransaction]
GO

-- 4. 添加新的外键约束
ALTER TABLE [dbo].[UsageTransaction] 
ADD CONSTRAINT [FK_UsageTransaction_Model] FOREIGN KEY([ModelId])
REFERENCES [dbo].[Model] ([Id])
GO

-- 5. 将新字段设置为非空
ALTER TABLE [dbo].[UsageTransaction] ALTER COLUMN [ModelId] [smallint] NOT NULL
GO

-- 6. 删除旧的UserModelId字段
ALTER TABLE [dbo].[UsageTransaction] DROP COLUMN [UserModelId]
GO

-- 7. 创建新的索引
CREATE NONCLUSTERED INDEX [IX_UsageTransaction_ModelId] ON [dbo].[UsageTransaction]
(
    [ModelId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO



-- 将UserModel表的IsDeleted字段删除（因为将UserModelUsage/UsageTransaction两个表外键引用去掉之后，UserModel表之后就可以安全删除了）
-- 1. 首先删除所有标记为已删除的记录
DELETE FROM [dbo].[UserModel]
WHERE [IsDeleted] = 1
GO

-- 2. 删除默认值约束（如果存在）
DECLARE @ConstraintName nvarchar(200)
SELECT @ConstraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('UserModel')
AND c.name = 'IsDeleted'

IF @ConstraintName IS NOT NULL
    EXEC('ALTER TABLE [dbo].[UserModel] DROP CONSTRAINT ' + @ConstraintName)
GO

-- 3. 删除 IsDeleted 字段
ALTER TABLE [dbo].[UserModel] DROP COLUMN [IsDeleted]
GO