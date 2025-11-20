-- =============================================
-- 1.9.0 数据库迁移脚本
-- 将 reasoning 内容从 StepContentText 抽离到 StepContentThink，并预留 Signature 字段
-- =============================================

PRINT N'[1.9.0] 开始迁移 StepContent reasoning 内容';

-- =============================================
-- 第一步：创建（或补全）StepContentThink 表
-- =============================================
PRINT N'[Step 1] 检查 StepContentThink 表';

IF OBJECT_ID(N'dbo.StepContentThink', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StepContentThink
    (
        Id BIGINT NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        Signature VARBINARY(MAX) NULL,
        CONSTRAINT PK_StepContentThink PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StepContentThink_StepContent FOREIGN KEY (Id) REFERENCES dbo.StepContent (Id)
    );

    PRINT N'    -> 已创建 StepContentThink 表';
END
ELSE
BEGIN
    PRINT N'    -> StepContentThink 表已存在，跳过创建';
END

-- =============================================
-- 第二步：迁移 ContentTypeId = 3 (reasoning) 的内容
-- =============================================
PRINT N'[Step 2] 迁移 reasoning 文本数据';

IF OBJECT_ID(N'dbo.StepContentThink', N'U') IS NULL
BEGIN
    RAISERROR(N'StepContentThink 表不存在，无法继续迁移', 16, 1);
    RETURN;
END

DECLARE @Inserted INT = 0;
DECLARE @Deleted INT = 0;

;WITH ReasoningStepContent AS
(
    SELECT sc.Id, sct.Content
    FROM dbo.StepContent sc
    INNER JOIN dbo.StepContentText sct ON sc.Id = sct.Id
    WHERE sc.ContentTypeId = 3
)
INSERT INTO dbo.StepContentThink (Id, Content)
SELECT r.Id, r.Content
FROM ReasoningStepContent r
WHERE NOT EXISTS (SELECT 1 FROM dbo.StepContentThink t WHERE t.Id = r.Id);

SET @Inserted = @@ROWCOUNT;
PRINT N'    -> 已插入 ' + CAST(@Inserted AS NVARCHAR(20)) + N' 条 StepContentThink 记录';

DELETE sct
FROM dbo.StepContentText sct
INNER JOIN dbo.StepContent sc ON sct.Id = sc.Id
WHERE sc.ContentTypeId = 3;

SET @Deleted = @@ROWCOUNT;
PRINT N'    -> 已从 StepContentText 删除 ' + CAST(@Deleted AS NVARCHAR(20)) + N' 条 reasoning 记录';

PRINT N'[1.9.0] 迁移完成';
GO
