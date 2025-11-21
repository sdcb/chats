PRINT N'[1.9.0] 开始执行数据库迁移任务';

-- =============================================
-- 第一步：创建（或补齐）StepContentThink 表
-- =============================================
PRINT N'[Step 1] 检查 StepContentThink 表结构';

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
    PRINT N'    -> StepContentThink 表已存在';
END

IF COL_LENGTH(N'dbo.StepContentThink', N'Signature') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentThink ADD Signature VARBINARY(MAX) NULL;
    PRINT N'    -> 已为 StepContentThink 添加 Signature 列';
END
ELSE
BEGIN
    PRINT N'    -> StepContentThink.Signature 已存在';
END

GO

-- =============================================
-- 第二步：迁移 ContentTypeId = 3 (reasoning) 的内容
-- =============================================
PRINT N'[Step 2] 迁移 reasoning 文本数据到 StepContentThink';

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

GO

-- =============================================
-- 第三步：移除 StepContentType 表及其外键
-- =============================================
PRINT N'[Step 3] 删除 StepContentType 外键与表';

DECLARE @fkStepContentType NVARCHAR(128);
SELECT @fkStepContentType = fk.name
FROM sys.foreign_key_columns fkc
INNER JOIN sys.objects fk ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables pt ON pt.object_id = fkc.parent_object_id
INNER JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
WHERE pt.name = N'StepContent' AND rt.name = N'StepContentType';

IF @fkStepContentType IS NOT NULL
BEGIN
    DECLARE @dropStepContentFk NVARCHAR(MAX) = N'ALTER TABLE dbo.StepContent DROP CONSTRAINT ' + QUOTENAME(@fkStepContentType);
    EXEC sp_executesql @dropStepContentFk;
    PRINT N'    -> 已删除 StepContent.ContentTypeId 外键约束';
END
ELSE
BEGIN
    PRINT N'    -> 未找到 StepContentType 外键，跳过';
END

IF OBJECT_ID(N'dbo.StepContentType', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.StepContentType;
    PRINT N'    -> 已删除 StepContentType 表';
END
ELSE
BEGIN
    PRINT N'    -> StepContentType 表不存在，跳过';
END

GO

-- =============================================
-- 第四步：File.MediaType 改造并删除 FileContentType 表
-- =============================================
PRINT N'[Step 4.1] 如果需要则新增 File.MediaType 列';

IF COL_LENGTH(N'dbo.[File]', N'MediaType') IS NULL
BEGIN
    ALTER TABLE dbo.[File] ADD MediaType NVARCHAR(100) NOT NULL CONSTRAINT DF_File_MediaType_190 DEFAULT(N'application/octet-stream');
    ALTER TABLE dbo.[File] DROP CONSTRAINT DF_File_MediaType_190;
    PRINT N'    -> 已新增 File.MediaType 列';
END
ELSE
BEGIN
    PRINT N'    -> File.MediaType 已存在';
END

GO

PRINT N'[Step 4.2] 用 FileContentType 数据填充新列';

IF COL_LENGTH(N'dbo.[File]', N'MediaType') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.FileContentType', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.[File]', N'FileContentTypeId') IS NOT NULL
    BEGIN
        UPDATE f
        SET MediaType = fct.ContentType
        FROM dbo.[File] f
        INNER JOIN dbo.FileContentType fct ON f.FileContentTypeId = fct.Id
        WHERE (f.MediaType IS NULL OR LTRIM(RTRIM(f.MediaType)) = N'' OR f.MediaType = N'application/octet-stream');

        PRINT N'    -> 已将 FileContentType 数据迁移到 File.MediaType';
    END
    ELSE
    BEGIN
        PRINT N'    -> FileContentType 表或 FileContentTypeId 列不存在，跳过数据迁移';
    END

    UPDATE dbo.[File]
    SET MediaType = N'application/octet-stream'
    WHERE MediaType IS NULL OR LTRIM(RTRIM(MediaType)) = N'';
END
ELSE
BEGIN
    PRINT N'    -> File.MediaType 列不存在，跳过填充';
END

GO

DECLARE @fkFileContentType NVARCHAR(128);
SELECT @fkFileContentType = fk.name
FROM sys.foreign_key_columns fkc
INNER JOIN sys.objects fk ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables pt ON pt.object_id = fkc.parent_object_id
INNER JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
WHERE pt.name = N'File' AND rt.name = N'FileContentType';

IF @fkFileContentType IS NOT NULL
BEGIN
    DECLARE @dropFileFk NVARCHAR(MAX) = N'ALTER TABLE dbo.[File] DROP CONSTRAINT ' + QUOTENAME(@fkFileContentType);
    EXEC sp_executesql @dropFileFk;
    PRINT N'    -> 已删除 File -> FileContentType 外键';
END
ELSE
BEGIN
    PRINT N'    -> 未找到 FileContentType 外键，跳过';
END

IF COL_LENGTH(N'dbo.[File]', N'FileContentTypeId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.[File] DROP COLUMN FileContentTypeId;
    PRINT N'    -> 已删除 File.FileContentTypeId 列';
END
ELSE
BEGIN
    PRINT N'    -> File.FileContentTypeId 列不存在，跳过';
END

IF OBJECT_ID(N'dbo.FileContentType', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.FileContentType;
    PRINT N'    -> 已删除 FileContentType 表';
END
ELSE
BEGIN
    PRINT N'    -> FileContentType 表不存在，跳过';
END

GO

-- =============================================
-- 第五步：为 StepContentBlob 增加媒体信息字段
-- =============================================
PRINT N'[Step 5.1] 检查 StepContentBlob 列结构';

IF COL_LENGTH(N'dbo.StepContentBlob', N'MediaType') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentBlob ADD MediaType NVARCHAR(100) NOT NULL CONSTRAINT DF_StepContentBlob_MediaType DEFAULT(N'application/octet-stream');
    ALTER TABLE dbo.StepContentBlob DROP CONSTRAINT DF_StepContentBlob_MediaType;
    PRINT N'    -> 已新增 StepContentBlob.MediaType 列';
END
ELSE
BEGIN
    PRINT N'    -> StepContentBlob.MediaType 已存在';
END

IF COL_LENGTH(N'dbo.StepContentBlob', N'FileName') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentBlob ADD FileName NVARCHAR(200) NULL;
    PRINT N'    -> 已新增 StepContentBlob.FileName 列';
END
ELSE
BEGIN
    PRINT N'    -> StepContentBlob.FileName 已存在';
END

GO

PRINT N'[Step 5.2] 为 StepContentBlob 填充默认 MediaType';

IF COL_LENGTH(N'dbo.StepContentBlob', N'MediaType') IS NOT NULL
BEGIN
    UPDATE dbo.StepContentBlob
    SET MediaType = N'application/octet-stream'
    WHERE MediaType IS NULL OR LTRIM(RTRIM(MediaType)) = N'';
END
ELSE
BEGIN
    PRINT N'    -> StepContentBlob.MediaType 列不存在，跳过填充';
END

GO

-- =============================================
-- 第六步：删除 ChatRole 表及 Step.ChatRole 外键
-- =============================================
PRINT N'[Step 6] 删除 Step -> ChatRole 外键并移除 ChatRole 表';

DECLARE @fkStepChatRole NVARCHAR(128);
SELECT @fkStepChatRole = fk.name
FROM sys.foreign_key_columns fkc
INNER JOIN sys.objects fk ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables pt ON pt.object_id = fkc.parent_object_id
INNER JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
WHERE pt.name = N'Step' AND rt.name = N'ChatRole';

IF @fkStepChatRole IS NOT NULL
BEGIN
    DECLARE @dropStepChatRole NVARCHAR(MAX) = N'ALTER TABLE dbo.Step DROP CONSTRAINT ' + QUOTENAME(@fkStepChatRole);
    EXEC sp_executesql @dropStepChatRole;
    PRINT N'    -> 已删除 Step.ChatRoleId 外键';
END
ELSE
BEGIN
    PRINT N'    -> 未找到 Step.ChatRole 外键，跳过';
END

IF OBJECT_ID(N'dbo.ChatRole', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChatRole;
    PRINT N'    -> 已删除 ChatRole 表';
END
ELSE
BEGIN
    PRINT N'    -> ChatRole 表不存在，跳过';
END

GO

-- =============================================
-- 第七步：Model/ChatConfig 表结构调整
-- =============================================
PRINT N'[Step 7.1] 删除 Model.AllowSystemPrompt 列';

IF COL_LENGTH(N'dbo.[Model]', N'AllowSystemPrompt') IS NOT NULL
BEGIN
    ALTER TABLE dbo.[Model] DROP COLUMN AllowSystemPrompt;
    PRINT N'    -> 已删除 Model.AllowSystemPrompt 列';
END
ELSE
BEGIN
    PRINT N'    -> Model.AllowSystemPrompt 列不存在，跳过';
END

GO

PRINT N'[Step 7.2] 新增 Model.MaxThinkingBudget 列';

IF COL_LENGTH(N'dbo.[Model]', N'MaxThinkingBudget') IS NULL
BEGIN
    ALTER TABLE dbo.[Model] ADD MaxThinkingBudget INT NULL;
    PRINT N'    -> 已新增 Model.MaxThinkingBudget 列';
END
ELSE
BEGIN
    PRINT N'    -> Model.MaxThinkingBudget 已存在，跳过';
END

GO

PRINT N'[Step 7.3] 新增 ChatConfig.ThinkingBudget 列';

IF COL_LENGTH(N'dbo.ChatConfig', N'ThinkingBudget') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfig ADD ThinkingBudget INT NULL;
    PRINT N'    -> 已新增 ChatConfig.ThinkingBudget 列';
END
ELSE
BEGIN
    PRINT N'    -> ChatConfig.ThinkingBudget 已存在，跳过';
END

GO

PRINT N'[Step 7.4] 调整 Model.ReasoningEffortOptions 与 SupportedImageSizes 可空设置';

IF COL_LENGTH(N'dbo.[Model]', N'ReasoningEffortOptions') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.[Model]')
          AND name = N'ReasoningEffortOptions'
          AND is_nullable = 0)
    BEGIN
        ALTER TABLE dbo.[Model] ALTER COLUMN ReasoningEffortOptions NVARCHAR(50) NULL;
        PRINT N'    -> 已将 Model.ReasoningEffortOptions 改为可空';
    END
    ELSE
    BEGIN
        PRINT N'    -> Model.ReasoningEffortOptions 已经为可空，跳过';
    END

    UPDATE dbo.[Model]
    SET ReasoningEffortOptions = NULL
    WHERE LTRIM(RTRIM(ISNULL(ReasoningEffortOptions, N''))) = N'';
END
ELSE
BEGIN
    PRINT N'    -> Model.ReasoningEffortOptions 列不存在，跳过';
END

IF COL_LENGTH(N'dbo.[Model]', N'SupportedImageSizes') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.[Model]')
          AND name = N'SupportedImageSizes'
          AND is_nullable = 0)
    BEGIN
        ALTER TABLE dbo.[Model] ALTER COLUMN SupportedImageSizes NVARCHAR(200) NULL;
        PRINT N'    -> 已将 Model.SupportedImageSizes 改为可空';
    END
    ELSE
    BEGIN
        PRINT N'    -> Model.SupportedImageSizes 已经为可空，跳过';
    END

    UPDATE dbo.[Model]
    SET SupportedImageSizes = NULL
    WHERE LTRIM(RTRIM(ISNULL(SupportedImageSizes, N''))) = N'';
END
ELSE
BEGIN
    PRINT N'    -> Model.SupportedImageSizes 列不存在，跳过';
END

GO

PRINT N'[1.9.0] 所有迁移步骤已完成';
GO
