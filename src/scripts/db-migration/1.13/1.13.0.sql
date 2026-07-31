PRINT N'[1.13.0] 开始执行 MCP ServerInstructions / UserMcp.ShowShortcut 迁移';

GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

GO

-- =============================================
-- Step 1: McpServer.ServerInstructions
-- MCP 全局提示词，对应 SDK McpClient.ServerInstructions
-- =============================================
PRINT N'[Step 1] McpServer.ServerInstructions';

IF COL_LENGTH(N'dbo.McpServer', N'ServerInstructions') IS NULL
BEGIN
    ALTER TABLE dbo.McpServer
    ADD ServerInstructions NVARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.McpServer.ServerInstructions';
END
ELSE
BEGIN
    PRINT N'    -> dbo.McpServer.ServerInstructions 已存在，跳过';
END

GO

-- =============================================
-- Step 2: UserMcp.ShowShortcut
-- 用户级是否在 ChatInput 显示该 MCP 快捷按钮；存量默认 0
-- =============================================
PRINT N'[Step 2] UserMcp.ShowShortcut';

IF COL_LENGTH(N'dbo.UserMcp', N'ShowShortcut') IS NULL
BEGIN
    -- DEFAULT 仅用于存量行回填，随后删除约束；新行默认值由应用层写入
    ALTER TABLE dbo.UserMcp
    ADD ShowShortcut BIT NOT NULL
        CONSTRAINT DF_UserMcp_ShowShortcut DEFAULT (0);

    ALTER TABLE dbo.UserMcp
    DROP CONSTRAINT DF_UserMcp_ShowShortcut;

    PRINT N'    -> 已新增 dbo.UserMcp.ShowShortcut';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserMcp.ShowShortcut 已存在，跳过';
END

GO

PRINT N'[1.13.0] 迁移完成';

GO
