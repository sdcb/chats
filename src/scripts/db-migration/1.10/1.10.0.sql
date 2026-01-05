PRINT N'[1.10.0] 开始执行数据库迁移任务';

-- =============================================
-- 第一步：创建 ChatDockerSession 表
-- =============================================
PRINT N'[Step 1] 创建 ChatDockerSession 表/外键/索引（若不存在）';

IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatDockerSession
    (
        Id BIGINT NOT NULL IDENTITY(1, 1),
        OwnerTurnId BIGINT NULL,
        Label NVARCHAR(64) NOT NULL,
        ContainerId NVARCHAR(128) NOT NULL,
        Image NVARCHAR(256) NOT NULL,
        ShellPrefix NVARCHAR(128) NOT NULL,
        Ip NVARCHAR(45) NULL,
        MemoryBytes BIGINT NULL,
        CpuCores REAL NULL,
        MaxProcesses SMALLINT NULL,
        NetworkMode TINYINT NOT NULL,
        TerminatedAt DATETIME2(7) NULL,
        CreatedAt DATETIME2(7) NOT NULL,
        LastActiveAt DATETIME2(7) NOT NULL,
        ExpiresAt DATETIME2(7) NOT NULL,

        CONSTRAINT PK_ChatDockerSession PRIMARY KEY CLUSTERED (Id)
    );

    PRINT N'    -> 已创建 ChatDockerSession 表';
END
ELSE
BEGIN
    PRINT N'    -> ChatDockerSession 表已存在，跳过创建';
END

IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ChatDockerSession_ChatTurn')
    BEGIN
        ALTER TABLE dbo.ChatDockerSession
        WITH CHECK ADD CONSTRAINT FK_ChatDockerSession_ChatTurn
        FOREIGN KEY (OwnerTurnId) REFERENCES dbo.ChatTurn(Id);

        ALTER TABLE dbo.ChatDockerSession CHECK CONSTRAINT FK_ChatDockerSession_ChatTurn;

        PRINT N'    -> 已创建外键 FK_ChatDockerSession_ChatTurn';
    END
    ELSE
    BEGIN
        PRINT N'    -> 外键 FK_ChatDockerSession_ChatTurn 已存在，跳过';
    END

    -- 索引：OwnerTurnId（用于按对话轮次回溯查找 session）
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ChatDockerSession_OwnerTurnId'
          AND object_id = OBJECT_ID(N'dbo.ChatDockerSession')
    )
    BEGIN
        CREATE INDEX IX_ChatDockerSession_OwnerTurnId
        ON dbo.ChatDockerSession (OwnerTurnId);

        PRINT N'    -> 已创建索引 IX_ChatDockerSession_OwnerTurnId';
    END
    ELSE
    BEGIN
        PRINT N'    -> 索引 IX_ChatDockerSession_OwnerTurnId 已存在，跳过';
    END

    -- 过滤索引：用于扫描未终结且已到期的 Session（WHERE TerminatedAt IS NULL AND ExpiresAt <= NOW）
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ChatDockerSession_Active_ExpiresAt'
          AND object_id = OBJECT_ID(N'dbo.ChatDockerSession')
    )
    BEGIN
        CREATE INDEX IX_ChatDockerSession_Active_ExpiresAt
        ON dbo.ChatDockerSession (ExpiresAt)
        INCLUDE (Id, ContainerId, OwnerTurnId, Label)
        WHERE TerminatedAt IS NULL;

        PRINT N'    -> 已创建过滤索引 IX_ChatDockerSession_Active_ExpiresAt';
    END
    ELSE
    BEGIN
        PRINT N'    -> 过滤索引 IX_ChatDockerSession_Active_ExpiresAt 已存在，跳过';
    END
END
ELSE
BEGIN
    PRINT N'    -> ChatDockerSession 表不存在，跳过外键与索引创建';
END

GO

-- =============================================
-- 第一步（1.1）：将 ChatDockerSession.OwnerTurnId 改为可空
-- =============================================
PRINT N'[Step 1.1] 将 ChatDockerSession.OwnerTurnId 置为可空（若当前为 NOT NULL）';

IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ChatDockerSession', N'OwnerTurnId') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.ChatDockerSession')
          AND name = N'OwnerTurnId'
          AND is_nullable = 0
    )
    BEGIN
        DECLARE @hadFk BIT = 0;

        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ChatDockerSession_ChatTurn')
        BEGIN
            SET @hadFk = 1;
            ALTER TABLE dbo.ChatDockerSession DROP CONSTRAINT FK_ChatDockerSession_ChatTurn;
            PRINT N'    -> 已移除外键 FK_ChatDockerSession_ChatTurn（用于修改列可空性）';
        END

        ALTER TABLE dbo.ChatDockerSession ALTER COLUMN OwnerTurnId BIGINT NULL;
        PRINT N'    -> 已将 ChatDockerSession.OwnerTurnId 修改为可空';

        IF @hadFk = 1
        BEGIN
            ALTER TABLE dbo.ChatDockerSession
            WITH CHECK ADD CONSTRAINT FK_ChatDockerSession_ChatTurn
            FOREIGN KEY (OwnerTurnId) REFERENCES dbo.ChatTurn(Id);

            ALTER TABLE dbo.ChatDockerSession CHECK CONSTRAINT FK_ChatDockerSession_ChatTurn;

            PRINT N'    -> 已恢复外键 FK_ChatDockerSession_ChatTurn';
        END
        ELSE
        BEGIN
            PRINT N'    -> 外键 FK_ChatDockerSession_ChatTurn 原本不存在，未恢复';
        END
    END
    ELSE
    BEGIN
        PRINT N'    -> OwnerTurnId 已是可空列，跳过';
    END
END
ELSE
BEGIN
    PRINT N'    -> ChatDockerSession 表或 OwnerTurnId 列不存在，跳过 Step 1.1';
END

GO

-- =============================================
-- 第一步（1.2）：添加 ShellPrefix 字段并迁移现有数据
-- =============================================
PRINT N'[Step 1.2] 添加 ShellPrefix 字段并迁移现有数据';

IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ChatDockerSession', N'ShellPrefix') IS NULL
BEGIN
    ALTER TABLE dbo.ChatDockerSession ADD ShellPrefix NVARCHAR(128) NOT NULL DEFAULT '/bin/bash,-lc';
    PRINT N'    -> 已添加 ShellPrefix 字段';
    
    -- 移除默认约束
    DECLARE @ConstraintName NVARCHAR(200);
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.ChatDockerSession')
      AND c.name = N'ShellPrefix';
    
    IF @ConstraintName IS NOT NULL
    BEGIN
        DECLARE @SqlDrop NVARCHAR(500);
        SET @SqlDrop = N'ALTER TABLE dbo.ChatDockerSession DROP CONSTRAINT ' + @ConstraintName;
        EXEC sp_executesql @SqlDrop;
        PRINT N'    -> 已移除临时默认约束';
    END
END
ELSE IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ChatDockerSession', N'ShellPrefix') IS NOT NULL
BEGIN
    PRINT N'    -> ShellPrefix 字段已存在，跳过添加';
    
    -- 更新现有空值记录（使用动态SQL避免编译时检查）
    DECLARE @SqlUpdate NVARCHAR(500);
    SET @SqlUpdate = N'UPDATE dbo.ChatDockerSession SET ShellPrefix = ''/bin/bash,-lc'' WHERE ShellPrefix IS NULL OR ShellPrefix = ''''';
    EXEC sp_executesql @SqlUpdate;
    
    PRINT N'    -> 已将现有空值记录的 ShellPrefix 更新为 /bin/bash,-lc';
END
ELSE
BEGIN
    PRINT N'    -> ChatDockerSession 表不存在，跳过 Step 1.2';
END

GO

-- =============================================
-- 第一步（1.3）：添加 Ip 字段（若不存在）
-- =============================================
PRINT N'[Step 1.3] 添加 Ip 字段（若不存在）';

IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ChatDockerSession', N'Ip') IS NULL
BEGIN
    ALTER TABLE dbo.ChatDockerSession ADD Ip NVARCHAR(45) NULL;
    PRINT N'    -> 已添加 Ip 字段';
END
ELSE IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ChatDockerSession', N'Ip') IS NOT NULL
BEGIN
    PRINT N'    -> Ip 字段已存在，跳过';
END
ELSE
BEGIN
    PRINT N'    -> ChatDockerSession 表不存在，跳过 Step 1.3';
END

GO

-- =============================================
-- 第二步：允许支持 ToolCall 的模型启用 Code Execution
-- =============================================
PRINT N'[Step 2] 将支持工具调用（AllowToolCall=1）的模型 AllowCodeExecution 置为 1';

IF OBJECT_ID(N'dbo.Model', N'U') IS NOT NULL
     AND COL_LENGTH(N'dbo.Model', N'AllowToolCall') IS NOT NULL
     AND COL_LENGTH(N'dbo.Model', N'AllowCodeExecution') IS NOT NULL
BEGIN
        UPDATE dbo.Model
        SET AllowCodeExecution = 1
        WHERE AllowToolCall = 1
            AND AllowCodeExecution = 0;

        PRINT N'    -> 已更新 Model.AllowCodeExecution（仅对 AllowToolCall=1 且当前为 0 的记录）';
END
ELSE
BEGIN
        PRINT N'    -> dbo.Model 或相关列不存在，跳过 Step 2';
END

GO

PRINT N'[1.10.0] 所有迁移步骤已完成';
GO
