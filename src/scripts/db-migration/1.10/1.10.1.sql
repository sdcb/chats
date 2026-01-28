PRINT N'[1.10.1] 开始执行数据库迁移任务';

-- =============================================
-- 扩展 ClientUserAgent.UserAgent 字段长度
-- =============================================
PRINT N'[Step 1] 扩展 ClientUserAgent.UserAgent 字段长度到 500';

-- 检查列长度是否不为 500
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.ClientUserAgent')
      AND c.name = N'UserAgent'
      AND c.max_length <> 500
)
BEGIN
    ALTER TABLE dbo.ClientUserAgent 
    ALTER COLUMN UserAgent VARCHAR(500) NOT NULL;
    
    PRINT N'    -> 已将 UserAgent 列长度扩展到 500';
END
ELSE
BEGIN
    PRINT N'    -> UserAgent 列长度已经是 500，跳过';
END

GO

-- =============================================
-- Step 2: 修改 ChatDockerSession 表
-- =============================================
PRINT N'[Step 2] 修改 ChatDockerSession 表：增加 OwnerChatId、更新外键约束';

-- 注意：SQLite 3.26.0+ 支持 ON DELETE SET NULL，但需要在创建时指定
-- 或者使用 PRAGMA foreign_keys = ON; 并重建表

-- 2.1 添加 OwnerChatId 列
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.ChatDockerSession') 
      AND name = N'OwnerChatId'
)
BEGIN
    ALTER TABLE dbo.ChatDockerSession
    ADD OwnerChatId INT NULL;
    
    PRINT N'    -> 已添加 OwnerChatId 列';
END
ELSE
BEGIN
    PRINT N'    -> OwnerChatId 列已存在，跳过';
END

GO

-- 2.2 创建 OwnerChatId 外键约束（NO ACTION，避免多级联路径冲突）
-- 注意：由于多级联路径限制，无法使用 ON DELETE SET NULL
-- 因此使用 NO ACTION，应用层代码 DeleteChats 中已处理 SET NULL 逻辑
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ChatDockerSession_Chat')
BEGIN
    ALTER TABLE dbo.ChatDockerSession
    WITH CHECK ADD CONSTRAINT FK_ChatDockerSession_Chat
    FOREIGN KEY (OwnerChatId) REFERENCES dbo.Chat(Id)
    ON DELETE NO ACTION;

    ALTER TABLE dbo.ChatDockerSession CHECK CONSTRAINT FK_ChatDockerSession_Chat;

    PRINT N'    -> 已创建 FK_ChatDockerSession_Chat 外键约束（NO ACTION）';
END
ELSE
BEGIN
    PRINT N'    -> FK_ChatDockerSession_Chat 外键约束已存在，跳过';
END

-- 2.3 创建索引
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatDockerSession_OwnerChatId'
      AND object_id = OBJECT_ID(N'dbo.ChatDockerSession')
)
BEGIN
    CREATE INDEX IX_ChatDockerSession_OwnerChatId
    ON dbo.ChatDockerSession (OwnerChatId);

    PRINT N'    -> 已创建索引 IX_ChatDockerSession_OwnerChatId';
END
ELSE
BEGIN
    PRINT N'    -> 索引 IX_ChatDockerSession_OwnerChatId 已存在，跳过';
END

-- 2.4 数据迁移：从 OwnerTurnId -> ChatTurn.ChatId 填充 OwnerChatId
UPDATE cds
SET cds.OwnerChatId = ct.ChatId
FROM dbo.ChatDockerSession cds
INNER JOIN dbo.ChatTurn ct ON cds.OwnerTurnId = ct.Id
WHERE cds.OwnerChatId IS NULL
  AND cds.OwnerTurnId IS NOT NULL;

PRINT N'    -> 已从 OwnerTurnId 关联的 ChatTurn 填充 OwnerChatId';

GO

PRINT N'[1.10.1] 数据库迁移任务完成';
