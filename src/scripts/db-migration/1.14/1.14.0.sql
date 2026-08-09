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

-- =============================================
-- Step 4: UserInitialConfig.Mcps
-- 保存新用户初始化时授予的 MCP 权限及其用户级配置。
-- 存量配置不授予任何 MCP；DEFAULT 仅用于回填，随后删除。
-- =============================================
PRINT N'[Step 4] UserInitialConfig.Mcps';

IF COL_LENGTH(N'dbo.UserInitialConfig', N'Mcps') IS NULL
BEGIN
    ALTER TABLE dbo.UserInitialConfig
    ADD Mcps NVARCHAR(MAX) NOT NULL
        CONSTRAINT DF_UserInitialConfig_Mcps DEFAULT (N'[]');

    ALTER TABLE dbo.UserInitialConfig
    DROP CONSTRAINT DF_UserInitialConfig_Mcps;

    PRINT N'    -> 已新增 dbo.UserInitialConfig.Mcps，存量配置设为 []';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserInitialConfig.Mcps 已存在，跳过';
END

GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.UserInitialConfig')
      AND name = N'CK_UserInitialConfig_Mcps_JsonArray'
)
BEGIN
    ALTER TABLE dbo.UserInitialConfig WITH CHECK
    ADD CONSTRAINT CK_UserInitialConfig_Mcps_JsonArray
        CHECK
        (
            ISJSON(Mcps) = 1
            AND LEFT(LTRIM(Mcps), 1) = N'['
        );

    PRINT N'    -> 已创建 CK_UserInitialConfig_Mcps_JsonArray';
END
ELSE
BEGIN
    PRINT N'    -> CK_UserInitialConfig_Mcps_JsonArray 已存在，跳过';
END

GO

-- =============================================
-- Step 5: UserInitialConfig.ApiKeyEnabled
-- 控制按该配置创建的新用户是否允许使用 API Key。
-- 存量配置保持启用；DEFAULT 仅用于回填，随后删除。
-- =============================================
PRINT N'[Step 5] UserInitialConfig.ApiKeyEnabled';

IF COL_LENGTH(N'dbo.UserInitialConfig', N'ApiKeyEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.UserInitialConfig
    ADD ApiKeyEnabled BIT NOT NULL
        CONSTRAINT DF_UserInitialConfig_ApiKeyEnabled DEFAULT (1);

    ALTER TABLE dbo.UserInitialConfig
    DROP CONSTRAINT DF_UserInitialConfig_ApiKeyEnabled;

    PRINT N'    -> 已新增 dbo.UserInitialConfig.ApiKeyEnabled，存量配置设为 1';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserInitialConfig.ApiKeyEnabled 已存在，跳过';
END

GO

-- =============================================
-- Step 6: MCP 标签改为同一所有者内唯一
-- 不同用户可以创建相同标签的 MCP；同一所有者仍不能创建重名 MCP。
-- 新复合索引同时覆盖按 OwnerUserId 查询，因此删除原单列索引。
-- =============================================
PRINT N'[Step 6] MCP 标签唯一范围';

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'UX_McpServer_Label'
)
BEGIN
    ALTER TABLE dbo.McpServer
    DROP CONSTRAINT UX_McpServer_Label;

    PRINT N'    -> 已删除全局标签唯一约束 UX_McpServer_Label';
END
ELSE IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'UX_McpServer_Label'
)
BEGIN
    DROP INDEX UX_McpServer_Label ON dbo.McpServer;

    PRINT N'    -> 已删除全局标签唯一索引 UX_McpServer_Label';
END
ELSE
BEGIN
    PRINT N'    -> UX_McpServer_Label 已不存在，跳过';
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'UX_McpServer_Owner_Label'
)
BEGIN
    CREATE UNIQUE INDEX UX_McpServer_Owner_Label
    ON dbo.McpServer(OwnerUserId, Label);

    PRINT N'    -> 已创建 UX_McpServer_Owner_Label';
END
ELSE
BEGIN
    PRINT N'    -> UX_McpServer_Owner_Label 已存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'IX_McpServer_OwnerUserId'
)
BEGIN
    DROP INDEX IX_McpServer_OwnerUserId ON dbo.McpServer;

    PRINT N'    -> 已删除被复合索引覆盖的 IX_McpServer_OwnerUserId';
END
ELSE
BEGIN
    PRINT N'    -> IX_McpServer_OwnerUserId 已不存在，跳过';
END

GO

PRINT N'[1.14.0] 迁移完成';

GO
