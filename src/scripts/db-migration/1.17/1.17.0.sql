PRINT N'[1.17.0] 开始执行图像生成 background 配置迁移';
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- ============================================================
-- Step 1: ChatConfig.Background
-- NULL 表示使用默认配置，请求中不显式发送 background；auto 是单独的显式值。
-- ============================================================
PRINT N'[Step 1] 新增 dbo.ChatConfig.Background';

IF OBJECT_ID(N'dbo.ChatConfig', N'U') IS NULL
BEGIN
    THROW 51700, N'[Step 1] dbo.ChatConfig 不存在，无法迁移', 1;
END

IF COL_LENGTH(N'dbo.ChatConfig', N'Background') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfig
        ADD Background VARCHAR(20) NULL;

    PRINT N'    -> 已新增 dbo.ChatConfig.Background';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatConfig.Background 已存在，跳过';
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_ChatConfig_Background'
      AND parent_object_id = OBJECT_ID(N'dbo.ChatConfig')
)
BEGIN
    ALTER TABLE dbo.ChatConfig WITH CHECK
        ADD CONSTRAINT CK_ChatConfig_Background
        CHECK (Background IS NULL OR Background IN ('transparent', 'opaque', 'auto'));

    ALTER TABLE dbo.ChatConfig CHECK CONSTRAINT CK_ChatConfig_Background;
    PRINT N'    -> 已创建 CK_ChatConfig_Background';
END
ELSE
BEGIN
    PRINT N'    -> CK_ChatConfig_Background 已存在，跳过';
END
GO

-- ============================================================
-- Step 2: ChatConfigSnapshot.Background
-- 历史快照同样保存该配置；存量快照保持 NULL。
-- ============================================================
PRINT N'[Step 2] 新增 dbo.ChatConfigSnapshot.Background';

IF OBJECT_ID(N'dbo.ChatConfigSnapshot', N'U') IS NULL
BEGIN
    THROW 51701, N'[Step 2] dbo.ChatConfigSnapshot 不存在，无法迁移', 1;
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Background') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfigSnapshot
        ADD Background VARCHAR(20) NULL;

    PRINT N'    -> 已新增 dbo.ChatConfigSnapshot.Background';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatConfigSnapshot.Background 已存在，跳过';
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_ChatConfigSnapshot_Background'
      AND parent_object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
)
BEGIN
    ALTER TABLE dbo.ChatConfigSnapshot WITH CHECK
        ADD CONSTRAINT CK_ChatConfigSnapshot_Background
        CHECK (Background IS NULL OR Background IN ('transparent', 'opaque', 'auto'));

    ALTER TABLE dbo.ChatConfigSnapshot CHECK CONSTRAINT CK_ChatConfigSnapshot_Background;
    PRINT N'    -> 已创建 CK_ChatConfigSnapshot_Background';
END
ELSE
BEGIN
    PRINT N'    -> CK_ChatConfigSnapshot_Background 已存在，跳过';
END
GO

-- ============================================================
-- Step 3: 迁移后校验
-- ============================================================
PRINT N'[Step 3] 执行迁移后校验';

IF COL_LENGTH(N'dbo.ChatConfig', N'Background') IS NULL
BEGIN
    THROW 51710, N'[Step 3] dbo.ChatConfig.Background 不存在', 1;
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Background') IS NULL
BEGIN
    THROW 51711, N'[Step 3] dbo.ChatConfigSnapshot.Background 不存在', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_ChatConfig_Background'
      AND parent_object_id = OBJECT_ID(N'dbo.ChatConfig')
)
BEGIN
    THROW 51712, N'[Step 3] CK_ChatConfig_Background 不存在', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_ChatConfigSnapshot_Background'
      AND parent_object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
)
BEGIN
    THROW 51713, N'[Step 3] CK_ChatConfigSnapshot_Background 不存在', 1;
END

SELECT
    OBJECT_SCHEMA_NAME(c.object_id) + N'.' + OBJECT_NAME(c.object_id) AS TableName,
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id IN
(
    OBJECT_ID(N'dbo.ChatConfig'),
    OBJECT_ID(N'dbo.ChatConfigSnapshot')
)
AND c.name = N'Background'
ORDER BY TableName;

PRINT N'[1.17.0] 图像生成 background 配置迁移完成';
GO
