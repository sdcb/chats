PRINT N'[1.15.0] 开始执行 MCP 工具元数据迁移';

GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

GO

-- =============================================
-- Step 1: McpServer.Name / DisplayName
-- 原 Label 保留为面向用户的 DisplayName，并允许为空；为空时应用层
-- 回退显示 Name。Name 用于组成可发送给大模型的工具名称，限制为
-- ASCII 字母、数字、下划线和短横线。
-- 存量 Name 使用稳定且全局唯一的 <Id>，避免对中文 Label 做
-- 不可靠的 SQL 音译或因清洗产生重名。
-- =============================================
PRINT N'[Step 1] McpServer.Name / DisplayName';

IF COL_LENGTH(N'dbo.McpServer', N'DisplayName') IS NULL
   AND COL_LENGTH(N'dbo.McpServer', N'Label') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.McpServer.Label', N'DisplayName', N'COLUMN';

    PRINT N'    -> 已将 dbo.McpServer.Label 重命名为 DisplayName';
END
ELSE IF COL_LENGTH(N'dbo.McpServer', N'DisplayName') IS NOT NULL
BEGIN
    PRINT N'    -> dbo.McpServer.DisplayName 已存在，跳过重命名';
END
ELSE
BEGIN
    THROW 50000, N'dbo.McpServer.Label 和 DisplayName 均不存在，无法迁移', 1;
END

GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'UX_McpServer_Owner_Label'
)
BEGIN
    DROP INDEX UX_McpServer_Owner_Label ON dbo.McpServer;

    PRINT N'    -> 已删除旧索引 UX_McpServer_Owner_Label';
END
ELSE
BEGIN
    PRINT N'    -> UX_McpServer_Owner_Label 已不存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'DisplayName'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.McpServer
    ALTER COLUMN DisplayName NVARCHAR(50) NULL;

    PRINT N'    -> 已将 dbo.McpServer.DisplayName 改为可空';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpServer.DisplayName 已为可空，跳过';
END

IF COL_LENGTH(N'dbo.McpServer', N'Name') IS NULL
BEGIN
    ALTER TABLE dbo.McpServer
    ADD Name VARCHAR(50) NULL;

    PRINT N'    -> 已新增 dbo.McpServer.Name';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpServer.Name 已存在，跳过新增';
END

GO

UPDATE dbo.McpServer
SET Name = CONVERT(VARCHAR(20), Id)
WHERE Name IS NULL OR Name = '';

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'Name'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.McpServer
    ALTER COLUMN Name VARCHAR(50) NOT NULL;

    PRINT N'    -> 已将 dbo.McpServer.Name 改为非空';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpServer.Name 已为非空，跳过';
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'UX_McpServer_Owner_Name'
)
BEGIN
    CREATE UNIQUE INDEX UX_McpServer_Owner_Name
    ON dbo.McpServer(OwnerUserId, Name);

    PRINT N'    -> 已创建 UX_McpServer_Owner_Name';
END
ELSE
BEGIN
    PRINT N'    -> UX_McpServer_Owner_Name 已存在，跳过';
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpServer')
      AND name = N'CK_McpServer_Name_SafeCharacters'
)
BEGIN
    ALTER TABLE dbo.McpServer WITH CHECK
    ADD CONSTRAINT CK_McpServer_Name_SafeCharacters
        CHECK
        (
            LEN(Name) BETWEEN 1 AND 50
            AND Name COLLATE Latin1_General_100_BIN2
                NOT LIKE '%[^A-Za-z0-9_-]%'
        );

    PRINT N'    -> 已创建 CK_McpServer_Name_SafeCharacters';
END
ELSE
BEGIN
    PRINT N'    -> CK_McpServer_Name_SafeCharacters 已存在，跳过';
END

GO

-- =============================================
-- Step 2: McpTool.Title
-- MCP 工具面向用户展示的友好名称；NULL 时由前端回退显示 ToolName。
-- =============================================
PRINT N'[Step 2] McpTool.Title';

IF COL_LENGTH(N'dbo.McpTool', N'Title') IS NULL
BEGIN
    ALTER TABLE dbo.McpTool
    ADD Title NVARCHAR(200) NULL;

    PRINT N'    -> 已新增 dbo.McpTool.Title';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpTool.Title 已存在，跳过';
END

GO

-- =============================================
-- Step 3: McpTool 行为配置
-- 字段由 MCP annotations 初始化，并可由 Chats 用户修改。
-- 服务端未声明及存量工具均按 false 处理。
-- DEFAULT 仅用于存量数据回填，随后删除约束；新行由应用层显式写入。
-- =============================================
PRINT N'[Step 3] McpTool.Destructive';

IF COL_LENGTH(N'dbo.McpTool', N'Destructive') IS NULL
BEGIN
    ALTER TABLE dbo.McpTool
    ADD Destructive BIT NOT NULL
        CONSTRAINT DF_McpTool_Destructive DEFAULT (0);

    PRINT N'    -> 已新增 dbo.McpTool.Destructive，存量工具设为 0';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpTool.Destructive 已存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpTool')
      AND name = N'DF_McpTool_Destructive'
)
BEGIN
    ALTER TABLE dbo.McpTool
    DROP CONSTRAINT DF_McpTool_Destructive;

    PRINT N'    -> 已删除临时默认约束 DF_McpTool_Destructive';
END

GO

PRINT N'[Step 4] McpTool.Idempotent';

IF COL_LENGTH(N'dbo.McpTool', N'Idempotent') IS NULL
BEGIN
    ALTER TABLE dbo.McpTool
    ADD Idempotent BIT NOT NULL
        CONSTRAINT DF_McpTool_Idempotent DEFAULT (0);

    PRINT N'    -> 已新增 dbo.McpTool.Idempotent，存量工具设为 0';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpTool.Idempotent 已存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpTool')
      AND name = N'DF_McpTool_Idempotent'
)
BEGIN
    ALTER TABLE dbo.McpTool
    DROP CONSTRAINT DF_McpTool_Idempotent;

    PRINT N'    -> 已删除临时默认约束 DF_McpTool_Idempotent';
END

GO

PRINT N'[Step 5] McpTool.OpenWorld';

IF COL_LENGTH(N'dbo.McpTool', N'OpenWorld') IS NULL
BEGIN
    ALTER TABLE dbo.McpTool
    ADD OpenWorld BIT NOT NULL
        CONSTRAINT DF_McpTool_OpenWorld DEFAULT (0);

    PRINT N'    -> 已新增 dbo.McpTool.OpenWorld，存量工具设为 0';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpTool.OpenWorld 已存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpTool')
      AND name = N'DF_McpTool_OpenWorld'
)
BEGIN
    ALTER TABLE dbo.McpTool
    DROP CONSTRAINT DF_McpTool_OpenWorld;

    PRINT N'    -> 已删除临时默认约束 DF_McpTool_OpenWorld';
END

GO

PRINT N'[Step 6] McpTool.ReadOnly';

IF COL_LENGTH(N'dbo.McpTool', N'ReadOnly') IS NULL
BEGIN
    ALTER TABLE dbo.McpTool
    ADD ReadOnly BIT NOT NULL
        CONSTRAINT DF_McpTool_ReadOnly DEFAULT (0);

    PRINT N'    -> 已新增 dbo.McpTool.ReadOnly，存量工具设为 0';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpTool.ReadOnly 已存在，跳过';
END

IF EXISTS
(
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.McpTool')
      AND name = N'DF_McpTool_ReadOnly'
)
BEGIN
    ALTER TABLE dbo.McpTool
    DROP CONSTRAINT DF_McpTool_ReadOnly;

    PRINT N'    -> 已删除临时默认约束 DF_McpTool_ReadOnly';
END

GO

-- =============================================
-- Step 7: StepContentToolCall.DisplayName
-- 保存工具调用发生时的展示名称快照；历史记录保持 NULL，
-- 由前端回退显示 Name。
-- =============================================
PRINT N'[Step 7] StepContentToolCall.DisplayName';

IF COL_LENGTH(N'dbo.StepContentToolCall', N'DisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentToolCall
    ADD DisplayName NVARCHAR(200) NULL;

    PRINT N'    -> 已新增 dbo.StepContentToolCall.DisplayName';
END
ELSE
BEGIN
    PRINT N'    -> dbo.StepContentToolCall.DisplayName 已存在，跳过';
END

GO

PRINT N'[1.15.0] 迁移完成';

GO
