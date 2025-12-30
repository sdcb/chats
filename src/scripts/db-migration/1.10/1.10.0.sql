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
        OwnerTurnId BIGINT NOT NULL,
        Label NVARCHAR(64) NOT NULL,
        ContainerId NVARCHAR(128) NOT NULL,
        Image NVARCHAR(256) NOT NULL,
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
