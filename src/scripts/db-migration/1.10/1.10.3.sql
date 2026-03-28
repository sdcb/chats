PRINT N'[1.10.3] 开始执行数据库迁移任务';

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

PRINT N'[1.10.3] 数据库迁移任务完成';

GO