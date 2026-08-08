PRINT N'[1.14.0] 开始执行用户消息上下文模板迁移';

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

PRINT N'[1.14.0] 迁移完成';

GO
