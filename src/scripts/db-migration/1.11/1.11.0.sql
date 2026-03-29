PRINT N'[1.11.0] 开始执行数据库迁移任务';

GO

-- =============================================
-- Step 1.1: 删除引用 TransactionType 的外键
-- =============================================
PRINT N'[Step 1.1] 删除引用 dbo.TransactionType 的外键（若存在）';

IF OBJECT_ID(N'dbo.TransactionType', N'U') IS NOT NULL
BEGIN
    DECLARE @DropForeignKeysSql NVARCHAR(MAX) = N'';

    SELECT @DropForeignKeysSql = @DropForeignKeysSql
        + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
        + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        + CHAR(13) + CHAR(10)
    FROM sys.foreign_keys fk
    WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.TransactionType');

    IF @DropForeignKeysSql <> N''
    BEGIN
        EXEC sys.sp_executesql @DropForeignKeysSql;
        PRINT N'    -> 已删除所有引用 dbo.TransactionType 的外键';
    END
    ELSE
    BEGIN
        PRINT N'    -> 未发现引用 dbo.TransactionType 的外键，跳过';
    END
END
ELSE
BEGIN
    PRINT N'    -> dbo.TransactionType 表不存在，跳过外键删除';
END

GO

-- =============================================
-- Step 1.2: 删除 TransactionType 表
-- =============================================
PRINT N'[Step 1.2] 删除 dbo.TransactionType 表（若存在）';

IF OBJECT_ID(N'dbo.TransactionType', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.TransactionType;
    PRINT N'    -> 已删除 dbo.TransactionType 表';
END
ELSE
BEGIN
    PRINT N'    -> dbo.TransactionType 表不存在，跳过';
END

GO

-- =============================================
-- Step 2.1: 为 UserModelUsage 增加 SourceId
-- UsageSource: WebChat = 1, Api = 2, Validate = 3
-- =============================================
PRINT N'[Step 2.1] 为 dbo.UserModelUsage 增加 SourceId';

IF COL_LENGTH(N'dbo.UserModelUsage', N'SourceId') IS NULL
BEGIN
    ALTER TABLE dbo.UserModelUsage
    ADD SourceId TINYINT NULL;

    PRINT N'    -> 已新增 dbo.UserModelUsage.SourceId（TINYINT NULL）';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserModelUsage.SourceId 已存在，跳过新增列';
END

GO

-- =============================================
-- Step 2.2: 回填历史数据
-- =============================================
PRINT N'[Step 2.2] 回填 dbo.UserModelUsage.SourceId 历史数据';

UPDATE umu
SET SourceId = 2
FROM dbo.UserModelUsage umu
INNER JOIN dbo.UserApiUsage uau ON uau.UsageId = umu.Id
WHERE umu.SourceId IS NULL;

PRINT N'    -> 已将存在 UserApiUsage 的历史记录回填为 Api(2)';

UPDATE umu
SET SourceId = 1
FROM dbo.UserModelUsage umu
LEFT JOIN dbo.UserApiUsage uau ON uau.UsageId = umu.Id
WHERE umu.SourceId IS NULL
  AND uau.UsageId IS NULL;

PRINT N'    -> 已将剩余历史记录回填为 WebChat(1)';

GO

-- =============================================
-- Step 2.3: 将 SourceId 收紧为 NOT NULL
-- =============================================
PRINT N'[Step 2.3] 将 dbo.UserModelUsage.SourceId 收紧为 NOT NULL';

IF EXISTS (
    SELECT 1
    FROM dbo.UserModelUsage
    WHERE SourceId IS NULL
)
BEGIN
    THROW 50001, N'[Step 2] dbo.UserModelUsage.SourceId 仍存在 NULL，无法收紧为 NOT NULL', 1;
END

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.UserModelUsage'), N'SourceId', 'AllowsNull') = 1
BEGIN
    ALTER TABLE dbo.UserModelUsage
    ALTER COLUMN SourceId TINYINT NOT NULL;

    PRINT N'    -> 已将 dbo.UserModelUsage.SourceId 收紧为 NOT NULL';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserModelUsage.SourceId 已是 NOT NULL，跳过';
END

GO

-- =============================================
-- Step 3: 创建 UserConfig 表
-- 用于用户级别的字符串配置，主键为 (UserId, Key)
-- =============================================
PRINT N'[Step 3] 创建 dbo.UserConfig 表';

CREATE TABLE dbo.UserConfig
(
    UserId INT NOT NULL,
    [Key] VARCHAR(100) NOT NULL,
    [Value] NVARCHAR(MAX) NOT NULL,
    [Description] NVARCHAR(50) NULL,

    CONSTRAINT PK_UserConfig PRIMARY KEY CLUSTERED (UserId, [Key]),
    CONSTRAINT FK_UserConfig_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id) ON DELETE CASCADE
);

PRINT N'    -> 已创建 dbo.UserConfig 表';

GO

PRINT N'[1.11.0] 数据库迁移任务完成';

GO