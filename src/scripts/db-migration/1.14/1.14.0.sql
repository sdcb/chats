PRINT N'[1.14.0] 开始执行数据库迁移';

GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

GO

-- =============================================
-- Step 1: StepContentText.ContextTemplate
-- NULL 表示模型上下文直接使用 Content；非 NULL 时由应用层
-- 使用 Content 替换模板中的内容占位符，生成模型实际接收的文本。
-- 历史数据无需回填，保持 NULL 即可维持原有行为。
-- =============================================
PRINT N'[Step 1] StepContentText.ContextTemplate';

IF COL_LENGTH(N'dbo.StepContentText', N'ContextTemplate') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentText
    ADD ContextTemplate NVARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.StepContentText.ContextTemplate';
END
ELSE
BEGIN
    PRINT N'    -> dbo.StepContentText.ContextTemplate 已存在，跳过';
END

GO

-- =============================================
-- Step 2: ChatPreset.IsSystem
-- 系统预设模型组由管理员维护，所有用户可见；现有预设均保持为
-- 用户私有预设。DEFAULT 仅用于存量数据回填，随后删除约束。
-- =============================================
PRINT N'[Step 2] ChatPreset.IsSystem';

IF COL_LENGTH(N'dbo.ChatPreset', N'IsSystem') IS NULL
BEGIN
    ALTER TABLE dbo.ChatPreset
    ADD IsSystem BIT NOT NULL
        CONSTRAINT DF_ChatPreset_IsSystem DEFAULT (0);

    ALTER TABLE dbo.ChatPreset
    DROP CONSTRAINT DF_ChatPreset_IsSystem;

    PRINT N'    -> 已新增 dbo.ChatPreset.IsSystem，存量预设设为 0';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatPreset.IsSystem 已存在，跳过';
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ChatPreset')
      AND name = N'IX_ChatPreset_IsSystem_Order'
)
BEGIN
    CREATE INDEX IX_ChatPreset_IsSystem_Order
    ON dbo.ChatPreset(IsSystem, [Order], Id)
    INCLUDE(UserId, Name, UpdatedAt);

    PRINT N'    -> 已创建 IX_ChatPreset_IsSystem_Order';
END
ELSE
BEGIN
    PRINT N'    -> IX_ChatPreset_IsSystem_Order 已存在，跳过';
END

GO

-- =============================================
-- Step 3: User.ApiKeyEnabled
-- 管理员可按用户禁止 API Key 功能；现有用户默认保持启用。
-- DEFAULT 仅用于存量数据回填，随后删除约束。
-- =============================================
PRINT N'[Step 3] User.ApiKeyEnabled';

IF COL_LENGTH(N'dbo.User', N'ApiKeyEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.[User]
    ADD ApiKeyEnabled BIT NOT NULL
        CONSTRAINT DF_User_ApiKeyEnabled DEFAULT (1);

    ALTER TABLE dbo.[User]
    DROP CONSTRAINT DF_User_ApiKeyEnabled;

    PRINT N'    -> 已新增 dbo.User.ApiKeyEnabled，存量用户设为 1';
END
ELSE
BEGIN
    PRINT N'    -> dbo.User.ApiKeyEnabled 已存在，跳过';
END

GO

PRINT N'[1.14.0] 迁移完成';

GO
