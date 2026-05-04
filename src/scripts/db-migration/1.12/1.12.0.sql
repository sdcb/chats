PRINT N'[1.12.0] 开始执行 1.12 数据迁移';

GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

GO

-- =============================================
-- Step 1.1: 扩表与建表
-- =============================================
PRINT N'[Step 1.1] 扩表与建表';

IF COL_LENGTH(N'dbo.ModelKey', N'CurrentSnapshotId') IS NULL
BEGIN
    ALTER TABLE dbo.ModelKey
    ADD CurrentSnapshotId INT NULL;

    PRINT N'    -> 已新增 dbo.ModelKey.CurrentSnapshotId';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelKey.CurrentSnapshotId 已存在，跳过';
END

IF COL_LENGTH(N'dbo.Model', N'CurrentSnapshotId') IS NULL
BEGIN
    ALTER TABLE dbo.Model
    ADD CurrentSnapshotId INT NULL;

    PRINT N'    -> 已新增 dbo.Model.CurrentSnapshotId';
END
ELSE
BEGIN
    PRINT N'    -> dbo.Model.CurrentSnapshotId 已存在，跳过';
END

IF COL_LENGTH(N'dbo.Model', N'Enabled') IS NULL
BEGIN
    ALTER TABLE dbo.Model
    ADD Enabled BIT NULL;

    PRINT N'    -> 已新增 dbo.Model.Enabled';
END
ELSE
BEGIN
    PRINT N'    -> dbo.Model.Enabled 已存在，跳过';
END

IF COL_LENGTH(N'dbo.ChatTurn', N'ChatConfigSnapshotId') IS NULL
BEGIN
    ALTER TABLE dbo.ChatTurn
    ADD ChatConfigSnapshotId INT NULL;

    PRINT N'    -> 已新增 dbo.ChatTurn.ChatConfigSnapshotId';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatTurn.ChatConfigSnapshotId 已存在，跳过';
END

IF COL_LENGTH(N'dbo.UsageTransaction', N'ModelSnapshotId') IS NULL
BEGIN
    ALTER TABLE dbo.UsageTransaction
    ADD ModelSnapshotId INT NULL;

    PRINT N'    -> 已新增 dbo.UsageTransaction.ModelSnapshotId';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UsageTransaction.ModelSnapshotId 已存在，跳过';
END

IF COL_LENGTH(N'dbo.UserModelUsage', N'ModelSnapshotId') IS NULL
BEGIN
    ALTER TABLE dbo.UserModelUsage
    ADD ModelSnapshotId INT NULL;

    PRINT N'    -> 已新增 dbo.UserModelUsage.ModelSnapshotId';
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserModelUsage.ModelSnapshotId 已存在，跳过';
END

IF OBJECT_ID(N'dbo.ModelKeySnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModelKeySnapshot
    (
        Id INT IDENTITY(1, 1) NOT NULL,
        ModelKeyId SMALLINT NOT NULL,
        ModelProviderId SMALLINT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        Host VARCHAR(500) NULL,
        Secret VARCHAR(1000) NULL,
        CustomHeaders VARCHAR(MAX) NULL,
        CustomBody VARCHAR(MAX) NULL,
        CreatedAt DATETIME2(7) NOT NULL,
        CONSTRAINT PK_ModelKeySnapshot PRIMARY KEY CLUSTERED (Id)
    );

    PRINT N'    -> 已创建 dbo.ModelKeySnapshot';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelKeySnapshot 已存在，跳过';
END

IF COL_LENGTH(N'dbo.ModelKeySnapshot', N'CustomHeaders') IS NULL
BEGIN
    ALTER TABLE dbo.ModelKeySnapshot
    ADD CustomHeaders VARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.ModelKeySnapshot.CustomHeaders';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelKeySnapshot.CustomHeaders 已存在，跳过';
END

IF COL_LENGTH(N'dbo.ModelKeySnapshot', N'CustomBody') IS NULL
BEGIN
    ALTER TABLE dbo.ModelKeySnapshot
    ADD CustomBody VARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.ModelKeySnapshot.CustomBody';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelKeySnapshot.CustomBody 已存在，跳过';
END

IF OBJECT_ID(N'dbo.ModelSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModelSnapshot
    (
        Id INT IDENTITY(1, 1) NOT NULL,
        ModelId SMALLINT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        DeploymentName NVARCHAR(100) NOT NULL,
        ModelKeyId SMALLINT NOT NULL,
        ModelKeySnapshotId INT NOT NULL,
        ApiTypeId TINYINT NOT NULL,
        InputFreshTokenPrice1M DECIMAL(9, 5) NOT NULL,
        InputCachedTokenPrice1M DECIMAL(9, 5) NOT NULL,
        OutputTokenPrice1M DECIMAL(9, 5) NOT NULL,
        AllowSearch BIT NOT NULL,
        AllowVision BIT NOT NULL,
        AllowStreaming BIT NOT NULL,
        AllowToolCall BIT NOT NULL,
        AllowCodeExecution BIT NOT NULL,
        ThinkTagParserEnabled BIT NOT NULL,
        MinTemperature DECIMAL(3, 2) NOT NULL,
        MaxTemperature DECIMAL(3, 2) NOT NULL,
        ContextWindow INT NOT NULL,
        MaxResponseTokens INT NOT NULL,
        ReasoningEffortOptions NVARCHAR(100) NULL,
        SupportedImageSizes NVARCHAR(400) NULL,
        UseAsyncApi BIT NOT NULL,
        UseMaxCompletionTokens BIT NOT NULL,
        IsLegacy BIT NOT NULL,
        MaxThinkingBudget INT NULL,
        SupportsVisionLink BIT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL,
        CONSTRAINT PK_ModelSnapshot PRIMARY KEY CLUSTERED (Id)
    );

    PRINT N'    -> 已创建 dbo.ModelSnapshot';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelSnapshot 已存在，跳过';
END

IF OBJECT_ID(N'dbo.ChatConfigSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatConfigSnapshot
    (
        Id INT IDENTITY(1, 1) NOT NULL,
        ModelSnapshotId INT NOT NULL,
        SystemPrompt NVARCHAR(MAX) NULL,
        Temperature REAL NULL,
        WebSearchEnabled BIT NOT NULL,
        MaxOutputTokens INT NULL,
        ReasoningEffortId TINYINT NOT NULL,
        CodeExecutionEnabled BIT NOT NULL,
        ImageSize NVARCHAR(40) NULL,
        ThinkingBudget INT NULL,
        EnabledMcpNames NVARCHAR(MAX) NULL,
        HashCode BIGINT NULL,
        CreatedAt DATETIME2(7) NOT NULL,
        CONSTRAINT PK_ChatConfigSnapshot PRIMARY KEY CLUSTERED (Id)
    );

    PRINT N'    -> 已创建 dbo.ChatConfigSnapshot';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatConfigSnapshot 已存在，跳过';
END

IF OBJECT_ID(N'dbo.__Migration_112_ChatConfigMap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__Migration_112_ChatConfigMap
    (
        ChatConfigId INT NOT NULL,
        ChatConfigSnapshotId INT NOT NULL,
        CONSTRAINT PK___Migration_112_ChatConfigMap PRIMARY KEY CLUSTERED (ChatConfigId)
    );

    PRINT N'    -> 已创建 dbo.__Migration_112_ChatConfigMap';
END
ELSE
BEGIN
    PRINT N'    -> dbo.__Migration_112_ChatConfigMap 已存在，跳过';
END

GO

-- =============================================
-- Step 1.2: 回填 ModelKeySnapshot
-- =============================================
PRINT N'[Step 1.2] 回填 dbo.ModelKeySnapshot';

IF COL_LENGTH(N'dbo.ModelKey', N'ModelProviderId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ModelKey', N'Name') IS NOT NULL
   AND COL_LENGTH(N'dbo.ModelKey', N'Host') IS NOT NULL
   AND COL_LENGTH(N'dbo.ModelKey', N'Secret') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
IF OBJECT_ID(N''tempdb..#InsertedModelKeySnapshot'', N''U'') IS NOT NULL
BEGIN
    DROP TABLE #InsertedModelKeySnapshot;
END

CREATE TABLE #InsertedModelKeySnapshot
(
    ModelKeyId SMALLINT NOT NULL PRIMARY KEY,
    ModelKeySnapshotId INT NOT NULL
);

DECLARE @ModelKeyId SMALLINT;
DECLARE @ModelProviderId SMALLINT;
DECLARE @ModelKeyName NVARCHAR(100);
DECLARE @ModelKeyHost VARCHAR(500);
DECLARE @ModelKeySecret VARCHAR(1000);
DECLARE @ModelKeyCreatedAt DATETIME2(7);
DECLARE @ModelKeySnapshotId INT;

DECLARE ModelKeyCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT mk.Id,
       mk.ModelProviderId,
       mk.[Name],
       mk.Host,
       mk.Secret,
       mk.CreatedAt
FROM dbo.ModelKey mk
WHERE mk.CurrentSnapshotId IS NULL
ORDER BY mk.Id;

OPEN ModelKeyCursor;

FETCH NEXT FROM ModelKeyCursor INTO @ModelKeyId, @ModelProviderId, @ModelKeyName, @ModelKeyHost, @ModelKeySecret, @ModelKeyCreatedAt;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO dbo.ModelKeySnapshot
    (
        ModelKeyId,
        ModelProviderId,
        [Name],
        Host,
        Secret,
        CreatedAt
    )
    VALUES
    (
        @ModelKeyId,
        @ModelProviderId,
        @ModelKeyName,
        @ModelKeyHost,
        @ModelKeySecret,
        @ModelKeyCreatedAt
    );

    SET @ModelKeySnapshotId = CAST(SCOPE_IDENTITY() AS INT);

    UPDATE dbo.ModelKey
    SET CurrentSnapshotId = @ModelKeySnapshotId
    WHERE Id = @ModelKeyId
      AND CurrentSnapshotId IS NULL;

    INSERT INTO #InsertedModelKeySnapshot (ModelKeyId, ModelKeySnapshotId)
    VALUES (@ModelKeyId, @ModelKeySnapshotId);

    FETCH NEXT FROM ModelKeyCursor INTO @ModelKeyId, @ModelProviderId, @ModelKeyName, @ModelKeyHost, @ModelKeySecret, @ModelKeyCreatedAt;
END

CLOSE ModelKeyCursor;
DEALLOCATE ModelKeyCursor;

PRINT N''    -> 已回填缺失的 dbo.ModelKeySnapshot'';

IF EXISTS (
    SELECT 1
    FROM dbo.ModelKey mk
    WHERE mk.CurrentSnapshotId IS NULL
)
BEGIN
    THROW 51200, N''[Step 1.2] 仍存在 CurrentSnapshotId 为空的 ModelKey'', 1;
END

PRINT N''    -> dbo.ModelKey.CurrentSnapshotId 校验通过'';';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ModelKey 已收缩，跳过旧结构回填';
END

GO

-- =============================================
-- Step 1.3: 回填 ModelSnapshot
-- =============================================
PRINT N'[Step 1.3] 回填 dbo.ModelSnapshot';

IF COL_LENGTH(N'dbo.Model', N'ModelKeyId') IS NOT NULL
   AND COL_LENGTH(N'dbo.Model', N'Name') IS NOT NULL
   AND COL_LENGTH(N'dbo.Model', N'DeploymentName') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
IF OBJECT_ID(N''tempdb..#InsertedModelSnapshot'', N''U'') IS NOT NULL
BEGIN
    DROP TABLE #InsertedModelSnapshot;
END

CREATE TABLE #InsertedModelSnapshot
(
    ModelId SMALLINT NOT NULL PRIMARY KEY,
    ModelSnapshotId INT NOT NULL
);

DECLARE @LiveModelId SMALLINT;
DECLARE @LiveModelKeyId SMALLINT;
DECLARE @LiveModelName NVARCHAR(100);
DECLARE @DeploymentName NVARCHAR(100);
DECLARE @InputFreshTokenPrice1M DECIMAL(9, 5);
DECLARE @InputCachedTokenPrice1M DECIMAL(9, 5);
DECLARE @OutputTokenPrice1M DECIMAL(9, 5);
DECLARE @AllowSearch BIT;
DECLARE @AllowVision BIT;
DECLARE @AllowStreaming BIT;
DECLARE @ThinkTagParserEnabled BIT;
DECLARE @MinTemperature DECIMAL(3, 2);
DECLARE @MaxTemperature DECIMAL(3, 2);
DECLARE @ContextWindow INT;
DECLARE @MaxResponseTokens INT;
DECLARE @AllowCodeExecution BIT;
DECLARE @ReasoningEffortOptions NVARCHAR(100);
DECLARE @AllowToolCall BIT;
DECLARE @SupportedImageSizes NVARCHAR(400);
DECLARE @ApiTypeId TINYINT;
DECLARE @UseAsyncApi BIT;
DECLARE @UseMaxCompletionTokens BIT;
DECLARE @IsLegacy BIT;
DECLARE @MaxThinkingBudget INT;
DECLARE @SupportsVisionLink BIT;
DECLARE @ModelCreatedAt DATETIME2(7);
DECLARE @CurrentModelKeySnapshotId INT;
DECLARE @CurrentModelSnapshotId INT;

DECLARE ModelCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT m.Id,
       m.ModelKeyId,
       m.[Name],
       m.DeploymentName,
       m.InputFreshTokenPrice1M,
       m.InputCachedTokenPrice1M,
       m.OutputTokenPrice1M,
       m.AllowSearch,
       m.AllowVision,
       m.AllowStreaming,
       m.ThinkTagParserEnabled,
       m.MinTemperature,
       m.MaxTemperature,
       m.ContextWindow,
       m.MaxResponseTokens,
       m.AllowCodeExecution,
       m.ReasoningEffortOptions,
       m.AllowToolCall,
       m.SupportedImageSizes,
       m.ApiTypeId,
       m.UseAsyncApi,
       m.UseMaxCompletionTokens,
       m.IsLegacy,
       m.MaxThinkingBudget,
       m.SupportsVisionLink,
       m.CreatedAt,
       mk.CurrentSnapshotId
FROM dbo.Model m
INNER JOIN dbo.ModelKey mk ON mk.Id = m.ModelKeyId
WHERE m.CurrentSnapshotId IS NULL
ORDER BY m.Id;

OPEN ModelCursor;

FETCH NEXT FROM ModelCursor INTO
    @LiveModelId,
    @LiveModelKeyId,
    @LiveModelName,
    @DeploymentName,
    @InputFreshTokenPrice1M,
    @InputCachedTokenPrice1M,
    @OutputTokenPrice1M,
    @AllowSearch,
    @AllowVision,
    @AllowStreaming,
    @ThinkTagParserEnabled,
    @MinTemperature,
    @MaxTemperature,
    @ContextWindow,
    @MaxResponseTokens,
    @AllowCodeExecution,
    @ReasoningEffortOptions,
    @AllowToolCall,
    @SupportedImageSizes,
    @ApiTypeId,
    @UseAsyncApi,
    @UseMaxCompletionTokens,
    @IsLegacy,
    @MaxThinkingBudget,
    @SupportsVisionLink,
    @ModelCreatedAt,
    @CurrentModelKeySnapshotId;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @CurrentModelKeySnapshotId IS NULL
    BEGIN
        THROW 51201, N''[Step 1.3] Model 对应的 ModelKey.CurrentSnapshotId 为空，无法回填 ModelSnapshot'', 1;
    END

    INSERT INTO dbo.ModelSnapshot
    (
        ModelId,
        [Name],
        DeploymentName,
        ModelKeyId,
        ModelKeySnapshotId,
        ApiTypeId,
        InputFreshTokenPrice1M,
        InputCachedTokenPrice1M,
        OutputTokenPrice1M,
        AllowSearch,
        AllowVision,
        AllowStreaming,
        AllowToolCall,
        AllowCodeExecution,
        ThinkTagParserEnabled,
        MinTemperature,
        MaxTemperature,
        ContextWindow,
        MaxResponseTokens,
        ReasoningEffortOptions,
        SupportedImageSizes,
        UseAsyncApi,
        UseMaxCompletionTokens,
        IsLegacy,
        MaxThinkingBudget,
        SupportsVisionLink,
        CreatedAt
    )
    VALUES
    (
        @LiveModelId,
        @LiveModelName,
        @DeploymentName,
        @LiveModelKeyId,
        @CurrentModelKeySnapshotId,
        @ApiTypeId,
        @InputFreshTokenPrice1M,
        @InputCachedTokenPrice1M,
        @OutputTokenPrice1M,
        @AllowSearch,
        @AllowVision,
        @AllowStreaming,
        @AllowToolCall,
        @AllowCodeExecution,
        @ThinkTagParserEnabled,
        @MinTemperature,
        @MaxTemperature,
        @ContextWindow,
        @MaxResponseTokens,
        @ReasoningEffortOptions,
        @SupportedImageSizes,
        @UseAsyncApi,
        @UseMaxCompletionTokens,
        @IsLegacy,
        @MaxThinkingBudget,
        @SupportsVisionLink,
        @ModelCreatedAt
    );

    SET @CurrentModelSnapshotId = CAST(SCOPE_IDENTITY() AS INT);

    UPDATE dbo.Model
    SET CurrentSnapshotId = @CurrentModelSnapshotId
    WHERE Id = @LiveModelId
      AND CurrentSnapshotId IS NULL;

    INSERT INTO #InsertedModelSnapshot (ModelId, ModelSnapshotId)
    VALUES (@LiveModelId, @CurrentModelSnapshotId);

    FETCH NEXT FROM ModelCursor INTO
        @LiveModelId,
        @LiveModelKeyId,
        @LiveModelName,
        @DeploymentName,
        @InputFreshTokenPrice1M,
        @InputCachedTokenPrice1M,
        @OutputTokenPrice1M,
        @AllowSearch,
        @AllowVision,
        @AllowStreaming,
        @ThinkTagParserEnabled,
        @MinTemperature,
        @MaxTemperature,
        @ContextWindow,
        @MaxResponseTokens,
        @AllowCodeExecution,
        @ReasoningEffortOptions,
        @AllowToolCall,
        @SupportedImageSizes,
        @ApiTypeId,
        @UseAsyncApi,
        @UseMaxCompletionTokens,
        @IsLegacy,
        @MaxThinkingBudget,
        @SupportsVisionLink,
        @ModelCreatedAt,
        @CurrentModelKeySnapshotId;
END

CLOSE ModelCursor;
DEALLOCATE ModelCursor;

PRINT N''    -> 已回填缺失的 dbo.ModelSnapshot'';

IF EXISTS (
    SELECT 1
    FROM dbo.Model m
    WHERE m.CurrentSnapshotId IS NULL
)
BEGIN
    THROW 51202, N''[Step 1.3] 仍存在 CurrentSnapshotId 为空的 Model'', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.ModelSnapshot ms
    WHERE ms.ModelKeySnapshotId IS NULL
)
BEGIN
    THROW 51203, N''[Step 1.3] 存在 ModelSnapshot.ModelKeySnapshotId 为空的记录'', 1;
END

PRINT N''    -> dbo.Model.CurrentSnapshotId 校验通过'';';
END
ELSE
BEGIN
    PRINT N'    -> dbo.Model 已收缩，跳过旧结构回填';
END

GO

-- =============================================
-- Step 1.4: 回填 ChatConfigSnapshot 与映射表
-- =============================================
PRINT N'[Step 1.4] 回填 dbo.ChatConfigSnapshot 与 dbo.__Migration_112_ChatConfigMap';

IF COL_LENGTH(N'dbo.ChatConfig', N'Id') IS NOT NULL
   AND COL_LENGTH(N'dbo.ChatTurn', N'ChatConfigId') IS NOT NULL
BEGIN
    MERGE dbo.ChatConfigSnapshot AS tgt
    USING
    (
        SELECT cc.Id AS ChatConfigId,
               m.CurrentSnapshotId AS ModelSnapshotId,
               cc.SystemPrompt,
               cc.Temperature,
               cc.WebSearchEnabled,
               cc.MaxOutputTokens,
               cc.ReasoningEffortId,
               cc.CodeExecutionEnabled,
               cc.ImageSize,
               cc.ThinkingBudget,
               mcp.EnabledMcpNames,
               CAST(NULL AS BIGINT) AS HashCode,
               SYSUTCDATETIME() AS CreatedAt
        FROM dbo.ChatConfig cc
        INNER JOIN dbo.Model m ON m.Id = cc.ModelId
        LEFT JOIN dbo.__Migration_112_ChatConfigMap map ON map.ChatConfigId = cc.Id
        OUTER APPLY
        (
            SELECT STRING_AGG(ms.Label, N',') WITHIN GROUP (ORDER BY ms.Label) AS EnabledMcpNames
            FROM dbo.ChatConfigMcp ccm
            INNER JOIN dbo.McpServer ms ON ms.Id = ccm.McpServerId
            WHERE ccm.ChatConfigId = cc.Id
        ) mcp
        WHERE map.ChatConfigId IS NULL
    ) AS src
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT
        (
            ModelSnapshotId,
            SystemPrompt,
            Temperature,
            WebSearchEnabled,
            MaxOutputTokens,
            ReasoningEffortId,
            CodeExecutionEnabled,
            ImageSize,
            ThinkingBudget,
            EnabledMcpNames,
            HashCode,
            CreatedAt
        )
        VALUES
        (
            src.ModelSnapshotId,
            src.SystemPrompt,
            src.Temperature,
            src.WebSearchEnabled,
            src.MaxOutputTokens,
            src.ReasoningEffortId,
            src.CodeExecutionEnabled,
            src.ImageSize,
            src.ThinkingBudget,
            src.EnabledMcpNames,
            src.HashCode,
            src.CreatedAt
        )
    OUTPUT src.ChatConfigId, inserted.Id
    INTO dbo.__Migration_112_ChatConfigMap (ChatConfigId, ChatConfigSnapshotId);

    PRINT N'    -> 已为尚未映射的 ChatConfig 创建快照';
END
ELSE
BEGIN
    PRINT N'    -> 旧列 dbo.ChatTurn.ChatConfigId 已不存在，跳过 ChatConfigSnapshot 初始回填';
END

IF COL_LENGTH(N'dbo.ChatTurn', N'ChatConfigId') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.ChatConfig cc
        LEFT JOIN dbo.__Migration_112_ChatConfigMap map ON map.ChatConfigId = cc.Id
        WHERE map.ChatConfigId IS NULL
    )
    BEGIN
        THROW 51204, N'[Step 1.4] 仍存在未建立快照映射的 ChatConfig', 1;
    END

    PRINT N'    -> dbo.ChatConfig 与 dbo.ChatConfigSnapshot 映射校验通过';
END
ELSE
BEGIN
    PRINT N'    -> 已完成收口后的库不再校验临时映射表';
END

GO

-- =============================================
-- Step 1.5: 回填 ChatTurn.ChatConfigSnapshotId
-- =============================================
PRINT N'[Step 1.5] 回填 dbo.ChatTurn.ChatConfigSnapshotId';

IF COL_LENGTH(N'dbo.ChatTurn', N'ChatConfigId') IS NOT NULL
BEGIN
    UPDATE ct
    SET ChatConfigSnapshotId = map.ChatConfigSnapshotId
    FROM dbo.ChatTurn ct
    INNER JOIN dbo.__Migration_112_ChatConfigMap map ON map.ChatConfigId = ct.ChatConfigId
    WHERE ct.ChatConfigId IS NOT NULL
      AND ct.ChatConfigSnapshotId IS NULL;

    PRINT N'    -> 已回填 dbo.ChatTurn.ChatConfigSnapshotId';

    IF EXISTS (
        SELECT 1
        FROM dbo.ChatTurn ct
        WHERE ct.ChatConfigId IS NOT NULL
          AND ct.ChatConfigSnapshotId IS NULL
    )
    BEGIN
        THROW 51205, N'[Step 1.5] 存在 ChatConfigId 非空但 ChatConfigSnapshotId 仍为空的 ChatTurn', 1;
    END
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatTurn.ChatConfigId 已不存在，跳过';
END

PRINT N'    -> dbo.ChatTurn.ChatConfigSnapshotId 校验通过';

GO

-- =============================================
-- Step 1.6: 回填 UsageTransaction / UserModelUsage 的 ModelSnapshotId
-- =============================================
PRINT N'[Step 1.6] 回填 dbo.UsageTransaction.ModelSnapshotId 与 dbo.UserModelUsage.ModelSnapshotId';

IF COL_LENGTH(N'dbo.UsageTransaction', N'ModelId') IS NOT NULL
BEGIN
    UPDATE ut
    SET ModelSnapshotId = m.CurrentSnapshotId
    FROM dbo.UsageTransaction ut
    INNER JOIN dbo.Model m ON m.Id = ut.ModelId
    WHERE ut.ModelSnapshotId IS NULL;

    IF EXISTS (
        SELECT 1
        FROM dbo.UsageTransaction ut
        WHERE ut.ModelSnapshotId IS NULL
    )
    BEGIN
        THROW 51206, N'[Step 1.6] 存在 UsageTransaction.ModelSnapshotId 仍为空的记录', 1;
    END
END
ELSE
BEGIN
    PRINT N'    -> dbo.UsageTransaction.ModelId 已不存在，跳过旧列回填';
END

IF COL_LENGTH(N'dbo.UserModelUsage', N'ModelId') IS NOT NULL
BEGIN
    UPDATE umu
    SET ModelSnapshotId = m.CurrentSnapshotId
    FROM dbo.UserModelUsage umu
    INNER JOIN dbo.Model m ON m.Id = umu.ModelId
    WHERE umu.ModelSnapshotId IS NULL;

    IF EXISTS (
        SELECT 1
        FROM dbo.UserModelUsage umu
        WHERE umu.ModelSnapshotId IS NULL
    )
    BEGIN
        THROW 51207, N'[Step 1.6] 存在 UserModelUsage.ModelSnapshotId 仍为空的记录', 1;
    END
END
ELSE
BEGIN
    PRINT N'    -> dbo.UserModelUsage.ModelId 已不存在，跳过旧列回填';
END

PRINT N'    -> dbo.UsageTransaction / dbo.UserModelUsage 回填校验通过';

GO

-- =============================================
-- Step 1.7: 数据收紧与一致性校验
-- =============================================
PRINT N'[Step 1.7] 数据收紧与一致性校验';

IF EXISTS (
    SELECT 1
    FROM dbo.ModelKey mk
    LEFT JOIN dbo.ModelKeySnapshot mks ON mks.Id = mk.CurrentSnapshotId
    WHERE mk.CurrentSnapshotId IS NULL
       OR mks.Id IS NULL
)
BEGIN
    THROW 51208, N'[Step 1.7] ModelKey.CurrentSnapshotId 无法完整解析到 ModelKeySnapshot', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.Model m
    LEFT JOIN dbo.ModelSnapshot ms ON ms.Id = m.CurrentSnapshotId
    WHERE m.CurrentSnapshotId IS NULL
       OR ms.Id IS NULL
)
BEGIN
    THROW 51209, N'[Step 1.7] Model.CurrentSnapshotId 无法完整解析到 ModelSnapshot', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.ModelSnapshot ms
    LEFT JOIN dbo.ModelKeySnapshot mks ON mks.Id = ms.ModelKeySnapshotId
    WHERE mks.Id IS NULL
)
BEGIN
    THROW 51210, N'[Step 1.7] ModelSnapshot.ModelKeySnapshotId 存在无效引用', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.ChatConfigSnapshot ccs
    LEFT JOIN dbo.ModelSnapshot ms ON ms.Id = ccs.ModelSnapshotId
    WHERE ms.Id IS NULL
)
BEGIN
    THROW 51211, N'[Step 1.7] ChatConfigSnapshot.ModelSnapshotId 存在无效引用', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.ChatTurn ct
    LEFT JOIN dbo.ChatConfigSnapshot ccs ON ccs.Id = ct.ChatConfigSnapshotId
    WHERE ct.ChatConfigSnapshotId IS NOT NULL
      AND ccs.Id IS NULL
)
BEGIN
    THROW 51212, N'[Step 1.7] ChatTurn.ChatConfigSnapshotId 存在无效引用', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.UsageTransaction ut
    LEFT JOIN dbo.ModelSnapshot ms ON ms.Id = ut.ModelSnapshotId
    WHERE ms.Id IS NULL
)
BEGIN
    THROW 51213, N'[Step 1.7] UsageTransaction.ModelSnapshotId 存在无效引用', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.UserModelUsage umu
    LEFT JOIN dbo.ModelSnapshot ms ON ms.Id = umu.ModelSnapshotId
    WHERE ms.Id IS NULL
)
BEGIN
    THROW 51214, N'[Step 1.7] UserModelUsage.ModelSnapshotId 存在无效引用', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.Model
    WHERE Enabled IS NULL
)
BEGIN
    UPDATE dbo.Model
    SET Enabled = CASE WHEN IsDeleted = 1 THEN 0 ELSE 1 END
    WHERE Enabled IS NULL;
END

IF EXISTS (
    SELECT 1
    FROM dbo.Model
    WHERE Enabled IS NULL
)
BEGIN
    THROW 51215, N'[Step 1.7] dbo.Model.Enabled 仍存在 NULL，无法收紧', 1;
END

PRINT N'    -> 一致性校验通过';

GO

-- =============================================
-- Step 1.8: 索引与外键
-- =============================================
PRINT N'[Step 1.8] 建立索引与外键';

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.ModelKey'), N'CurrentSnapshotId', 'AllowsNull') = 1
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ModelKey')
          AND name = N'UX_ModelKey_CurrentSnapshotId'
    )
    BEGIN
        DROP INDEX UX_ModelKey_CurrentSnapshotId ON dbo.ModelKey;
    END

    ALTER TABLE dbo.ModelKey
    ALTER COLUMN CurrentSnapshotId INT NOT NULL;
END

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Model'), N'CurrentSnapshotId', 'AllowsNull') = 1
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Model')
          AND name = N'UX_Model_CurrentSnapshotId'
    )
    BEGIN
        DROP INDEX UX_Model_CurrentSnapshotId ON dbo.Model;
    END

    ALTER TABLE dbo.Model
    ALTER COLUMN CurrentSnapshotId INT NOT NULL;
END

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Model'), N'Enabled', 'AllowsNull') = 1
BEGIN
    ALTER TABLE dbo.Model
    ALTER COLUMN Enabled BIT NOT NULL;
END

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.UsageTransaction'), N'ModelSnapshotId', 'AllowsNull') = 1
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.UsageTransaction')
          AND name = N'IX_UsageTransaction_ModelSnapshotId'
    )
    BEGIN
        DROP INDEX IX_UsageTransaction_ModelSnapshotId ON dbo.UsageTransaction;
    END

    ALTER TABLE dbo.UsageTransaction
    ALTER COLUMN ModelSnapshotId INT NOT NULL;
END

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.UserModelUsage'), N'ModelSnapshotId', 'AllowsNull') = 1
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.UserModelUsage')
          AND name = N'IX_UserModelUsage_ModelSnapshotId'
    )
    BEGIN
        DROP INDEX IX_UserModelUsage_ModelSnapshotId ON dbo.UserModelUsage;
    END

    ALTER TABLE dbo.UserModelUsage
    ALTER COLUMN ModelSnapshotId INT NOT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelKeySnapshot')
      AND name = N'IX_ModelKeySnapshot_ModelKeyId'
)
BEGIN
    CREATE INDEX IX_ModelKeySnapshot_ModelKeyId
    ON dbo.ModelKeySnapshot (ModelKeyId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelSnapshot')
      AND name = N'IX_ModelSnapshot_ModelId'
)
BEGIN
    CREATE INDEX IX_ModelSnapshot_ModelId
    ON dbo.ModelSnapshot (ModelId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelSnapshot')
      AND name = N'IX_ModelSnapshot_ModelKeyId'
)
BEGIN
    CREATE INDEX IX_ModelSnapshot_ModelKeyId
    ON dbo.ModelSnapshot (ModelKeyId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelSnapshot')
      AND name = N'IX_ModelSnapshot_ModelKeySnapshotId'
)
BEGIN
    CREATE INDEX IX_ModelSnapshot_ModelKeySnapshotId
    ON dbo.ModelSnapshot (ModelKeySnapshotId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
      AND name = N'IX_ChatConfigSnapshot_ModelSnapshotId'
)
BEGIN
    CREATE INDEX IX_ChatConfigSnapshot_ModelSnapshotId
    ON dbo.ChatConfigSnapshot (ModelSnapshotId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
      AND name = N'IX_ChatConfigSnapshot_HashCode'
)
BEGIN
    CREATE INDEX IX_ChatConfigSnapshot_HashCode
    ON dbo.ChatConfigSnapshot (HashCode)
    WHERE HashCode IS NOT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
      AND name = N'UX_ChatConfigSnapshot_ModelSnapshotId_HashCode'
)
BEGIN
    CREATE UNIQUE INDEX UX_ChatConfigSnapshot_ModelSnapshotId_HashCode
    ON dbo.ChatConfigSnapshot (ModelSnapshotId, HashCode)
    WHERE HashCode IS NOT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ChatTurn')
      AND name = N'IX_ChatTurn_ChatConfigSnapshotId'
)
BEGIN
    CREATE INDEX IX_ChatTurn_ChatConfigSnapshotId
    ON dbo.ChatTurn (ChatConfigSnapshotId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.UsageTransaction')
      AND name = N'IX_UsageTransaction_ModelSnapshotId'
)
BEGIN
    CREATE INDEX IX_UsageTransaction_ModelSnapshotId
    ON dbo.UsageTransaction (ModelSnapshotId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.UserModelUsage')
      AND name = N'IX_UserModelUsage_ModelSnapshotId'
)
BEGIN
    CREATE INDEX IX_UserModelUsage_ModelSnapshotId
    ON dbo.UserModelUsage (ModelSnapshotId);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelKey')
      AND name = N'UX_ModelKey_CurrentSnapshotId'
)
BEGIN
    CREATE UNIQUE INDEX UX_ModelKey_CurrentSnapshotId
    ON dbo.ModelKey (CurrentSnapshotId)
    WHERE CurrentSnapshotId IS NOT NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Model')
      AND name = N'UX_Model_CurrentSnapshotId'
)
BEGIN
    CREATE UNIQUE INDEX UX_Model_CurrentSnapshotId
    ON dbo.Model (CurrentSnapshotId)
    WHERE CurrentSnapshotId IS NOT NULL;
END

IF OBJECT_ID(N'dbo.FK_ModelKey_CurrentSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ModelKey
    WITH CHECK ADD CONSTRAINT FK_ModelKey_CurrentSnapshot
    FOREIGN KEY (CurrentSnapshotId) REFERENCES dbo.ModelKeySnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_Model_CurrentSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.Model
    WITH CHECK ADD CONSTRAINT FK_Model_CurrentSnapshot
    FOREIGN KEY (CurrentSnapshotId) REFERENCES dbo.ModelSnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_ModelSnapshot_ModelKeySnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ModelSnapshot
    WITH CHECK ADD CONSTRAINT FK_ModelSnapshot_ModelKeySnapshot
    FOREIGN KEY (ModelKeySnapshotId) REFERENCES dbo.ModelKeySnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_ChatConfigSnapshot_ModelSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfigSnapshot
    WITH CHECK ADD CONSTRAINT FK_ChatConfigSnapshot_ModelSnapshot
    FOREIGN KEY (ModelSnapshotId) REFERENCES dbo.ModelSnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_ChatTurn_ChatConfigSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ChatTurn
    WITH CHECK ADD CONSTRAINT FK_ChatTurn_ChatConfigSnapshot
    FOREIGN KEY (ChatConfigSnapshotId) REFERENCES dbo.ChatConfigSnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_UsageTransaction_ModelSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.UsageTransaction
    WITH CHECK ADD CONSTRAINT FK_UsageTransaction_ModelSnapshot
    FOREIGN KEY (ModelSnapshotId) REFERENCES dbo.ModelSnapshot(Id);
END

IF OBJECT_ID(N'dbo.FK_UserModelUsage_ModelSnapshot', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.UserModelUsage
    WITH CHECK ADD CONSTRAINT FK_UserModelUsage_ModelSnapshot
    FOREIGN KEY (ModelSnapshotId) REFERENCES dbo.ModelSnapshot(Id);
END

PRINT N'    -> 索引与外键建立完成';

GO

-- =============================================
-- Step 1.9: 删除旧外键、旧索引、旧列和旧表
-- =============================================
PRINT N'[Step 1.9] 删除旧外键、旧索引、旧列和旧表';

IF OBJECT_ID(N'dbo.FK_ChatTurn_ChatConfig', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatTurn DROP CONSTRAINT FK_ChatTurn_ChatConfig;
END

IF OBJECT_ID(N'dbo.FK_UsageTransaction_Model', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UsageTransaction DROP CONSTRAINT FK_UsageTransaction_Model;
END

IF OBJECT_ID(N'dbo.FK_UserModelUsage_Model', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserModelUsage DROP CONSTRAINT FK_UserModelUsage_Model;
END

IF OBJECT_ID(N'dbo.FK_Model_ModelKey2', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP CONSTRAINT FK_Model_ModelKey2;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.UsageTransaction')
      AND name = N'IX_UsageTransaction_ModelId'
)
BEGIN
    DROP INDEX IX_UsageTransaction_ModelId ON dbo.UsageTransaction;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.UserModelUsage')
      AND name = N'IX_UserModelUsage_ModelId'
)
BEGIN
    DROP INDEX IX_UserModelUsage_ModelId ON dbo.UserModelUsage;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Model')
      AND name = N'IX_Model_ModelKeyId'
)
BEGIN
    DROP INDEX IX_Model_ModelKeyId ON dbo.Model;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Model')
      AND name = N'IX_Model_Name'
)
BEGIN
    DROP INDEX IX_Model_Name ON dbo.Model;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelKey')
      AND name = N'IX_ModelKey2_ModelProviderId'
)
BEGIN
    DROP INDEX IX_ModelKey2_ModelProviderId ON dbo.ModelKey;
END

IF COL_LENGTH(N'dbo.ChatTurn', N'ChatConfigId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatTurn DROP COLUMN ChatConfigId;
END

IF COL_LENGTH(N'dbo.UsageTransaction', N'ModelId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UsageTransaction DROP COLUMN ModelId;
END

IF COL_LENGTH(N'dbo.UserModelUsage', N'ModelId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserModelUsage DROP COLUMN ModelId;
END

IF COL_LENGTH(N'dbo.Model', N'ModelKeyId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN ModelKeyId;
END

IF COL_LENGTH(N'dbo.Model', N'Name') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN [Name];
END

IF COL_LENGTH(N'dbo.Model', N'DeploymentName') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN DeploymentName;
END

IF COL_LENGTH(N'dbo.Model', N'InputFreshTokenPrice1M') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN InputFreshTokenPrice1M;
END

IF COL_LENGTH(N'dbo.Model', N'InputCachedTokenPrice1M') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN InputCachedTokenPrice1M;
END

IF COL_LENGTH(N'dbo.Model', N'OutputTokenPrice1M') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN OutputTokenPrice1M;
END

IF COL_LENGTH(N'dbo.Model', N'IsDeleted') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN IsDeleted;
END

IF COL_LENGTH(N'dbo.Model', N'AllowSearch') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN AllowSearch;
END

IF COL_LENGTH(N'dbo.Model', N'AllowVision') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN AllowVision;
END

IF COL_LENGTH(N'dbo.Model', N'AllowStreaming') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN AllowStreaming;
END

IF COL_LENGTH(N'dbo.Model', N'ThinkTagParserEnabled') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN ThinkTagParserEnabled;
END

IF COL_LENGTH(N'dbo.Model', N'MinTemperature') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN MinTemperature;
END

IF COL_LENGTH(N'dbo.Model', N'MaxTemperature') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN MaxTemperature;
END

IF COL_LENGTH(N'dbo.Model', N'ContextWindow') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN ContextWindow;
END

IF COL_LENGTH(N'dbo.Model', N'MaxResponseTokens') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN MaxResponseTokens;
END

IF COL_LENGTH(N'dbo.Model', N'AllowCodeExecution') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN AllowCodeExecution;
END

IF COL_LENGTH(N'dbo.Model', N'ReasoningEffortOptions') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN ReasoningEffortOptions;
END

IF COL_LENGTH(N'dbo.Model', N'AllowToolCall') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN AllowToolCall;
END

IF COL_LENGTH(N'dbo.Model', N'SupportedImageSizes') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN SupportedImageSizes;
END

IF COL_LENGTH(N'dbo.Model', N'ApiTypeId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN ApiTypeId;
END

IF COL_LENGTH(N'dbo.Model', N'UseAsyncApi') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN UseAsyncApi;
END

IF COL_LENGTH(N'dbo.Model', N'UseMaxCompletionTokens') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN UseMaxCompletionTokens;
END

IF COL_LENGTH(N'dbo.Model', N'IsLegacy') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN IsLegacy;
END

IF COL_LENGTH(N'dbo.Model', N'MaxThinkingBudget') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN MaxThinkingBudget;
END

IF COL_LENGTH(N'dbo.Model', N'SupportsVisionLink') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Model DROP COLUMN SupportsVisionLink;
END

IF COL_LENGTH(N'dbo.ModelKey', N'ModelProviderId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ModelKey DROP COLUMN ModelProviderId;
END

IF COL_LENGTH(N'dbo.ModelKey', N'Name') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ModelKey DROP COLUMN [Name];
END

IF COL_LENGTH(N'dbo.ModelKey', N'Host') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ModelKey DROP COLUMN Host;
END

IF COL_LENGTH(N'dbo.ModelKey', N'Secret') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ModelKey DROP COLUMN Secret;
END

IF OBJECT_ID(N'dbo.ChatConfigArchived', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChatConfigArchived;
END

IF OBJECT_ID(N'dbo.__Migration_112_ChatConfigMap', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.__Migration_112_ChatConfigMap;
END

PRINT N'    -> 旧结构已清理';

GO

-- =============================================
-- Step 1.10: 收口后校验
-- =============================================
PRINT N'[Step 1.10] 收口后校验';

IF EXISTS (
    SELECT 1
    FROM dbo.ModelKey mk
    LEFT JOIN dbo.ModelKeySnapshot mks ON mks.Id = mk.CurrentSnapshotId
    WHERE mks.Id IS NULL
)
BEGIN
    THROW 51216, N'[Step 1.10] 收口后仍存在无法解析的 ModelKey.CurrentSnapshotId', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.Model m
    LEFT JOIN dbo.ModelSnapshot ms ON ms.Id = m.CurrentSnapshotId
    LEFT JOIN dbo.ModelKey mk ON mk.Id = ms.ModelKeyId
    LEFT JOIN dbo.ModelKeySnapshot mks ON mks.Id = mk.CurrentSnapshotId
    WHERE ms.Id IS NULL
       OR mk.Id IS NULL
       OR mks.Id IS NULL
)
BEGIN
    THROW 51217, N'[Step 1.10] 收口后仍存在无法沿 live 链路解析的 Model 当前版本', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.ChatTurn ct
    WHERE ct.ChatConfigSnapshotId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.ChatConfigSnapshot ccs
          WHERE ccs.Id = ct.ChatConfigSnapshotId
      )
)
BEGIN
    THROW 51218, N'[Step 1.10] 收口后仍存在无效的 ChatTurn.ChatConfigSnapshotId', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.UsageTransaction ut
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.ModelSnapshot ms
        WHERE ms.Id = ut.ModelSnapshotId
    )
)
BEGIN
    THROW 51219, N'[Step 1.10] 收口后仍存在无效的 UsageTransaction.ModelSnapshotId', 1;
END

IF EXISTS (
    SELECT 1
    FROM dbo.UserModelUsage umu
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.ModelSnapshot ms
        WHERE ms.Id = umu.ModelSnapshotId
    )
)
BEGIN
    THROW 51220, N'[Step 1.10] 收口后仍存在无效的 UserModelUsage.ModelSnapshotId', 1;
END

PRINT N'    -> 收口后校验通过';

GO

-- =============================================
-- Step 2: Model / ChatConfig 配置迁移
-- =============================================
PRINT N'[Step 2] 执行 Model / ChatConfig 配置迁移';

IF COL_LENGTH(N'dbo.ModelSnapshot', N'ReasoningEffortOptions') IS NOT NULL
   AND COL_LENGTH(N'dbo.ModelSnapshot', N'SupportedEfforts') IS NULL
BEGIN
    EXEC sp_rename N'dbo.ModelSnapshot.ReasoningEffortOptions', N'SupportedEfforts', N'COLUMN';
    PRINT N'    -> 已将 dbo.ModelSnapshot.ReasoningEffortOptions 重命名为 SupportedEfforts';
END

IF COL_LENGTH(N'dbo.ModelSnapshot', N'SupportedFormats') IS NULL
BEGIN
    ALTER TABLE dbo.ModelSnapshot
    ADD SupportedFormats VARCHAR(100) NULL;

    PRINT N'    -> 已新增 dbo.ModelSnapshot.SupportedFormats';
END

IF COL_LENGTH(N'dbo.ModelSnapshot', N'OverrideUrl') IS NULL
BEGIN
    ALTER TABLE dbo.ModelSnapshot
    ADD OverrideUrl VARCHAR(1000) NULL;

    PRINT N'    -> 已新增 dbo.ModelSnapshot.OverrideUrl';
END

IF COL_LENGTH(N'dbo.ModelSnapshot', N'CustomHeaders') IS NULL
BEGIN
    ALTER TABLE dbo.ModelSnapshot
    ADD CustomHeaders VARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.ModelSnapshot.CustomHeaders';
END

IF COL_LENGTH(N'dbo.ModelSnapshot', N'CustomBody') IS NULL
BEGIN
    ALTER TABLE dbo.ModelSnapshot
    ADD CustomBody VARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 dbo.ModelSnapshot.CustomBody';
END

IF COL_LENGTH(N'dbo.ChatConfig', N'ReasoningEffortId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ChatConfig', N'Effort') IS NULL
BEGIN
    EXEC sp_rename N'dbo.ChatConfig.ReasoningEffortId', N'Effort', N'COLUMN';
    PRINT N'    -> 已将 dbo.ChatConfig.ReasoningEffortId 重命名为 Effort';
END

IF COL_LENGTH(N'dbo.ChatConfig', N'Format') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfig
    ADD [Format] VARCHAR(20) NULL;

    PRINT N'    -> 已新增 dbo.ChatConfig.Format';
END

IF COL_LENGTH(N'dbo.ChatConfig', N'Compression') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfig
    ADD Compression TINYINT NULL;

    PRINT N'    -> 已新增 dbo.ChatConfig.Compression';
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'ReasoningEffortId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Effort') IS NULL
BEGIN
    EXEC sp_rename N'dbo.ChatConfigSnapshot.ReasoningEffortId', N'Effort', N'COLUMN';
    PRINT N'    -> 已将 dbo.ChatConfigSnapshot.ReasoningEffortId 重命名为 Effort';
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Format') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfigSnapshot
    ADD [Format] VARCHAR(20) NULL;

    PRINT N'    -> 已新增 dbo.ChatConfigSnapshot.Format';
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Compression') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfigSnapshot
    ADD Compression TINYINT NULL;

    PRINT N'    -> 已新增 dbo.ChatConfigSnapshot.Compression';
END

GO

PRINT N'[Step 2] 规范化 Model / ChatConfig 配置字段';

IF COL_LENGTH(N'dbo.ModelSnapshot', N'SupportedEfforts') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.ModelSnapshot ms
        CROSS APPLY OPENJSON(
            CONCAT(
                '["',
                REPLACE(REPLACE(REPLACE(CAST(ms.SupportedEfforts AS NVARCHAR(MAX)), N'\', N'\\'), N'"', N'\"'), N',', N'","'),
                '"]'
            )
        ) j
        WHERE NULLIF(LTRIM(RTRIM(CAST(ms.SupportedEfforts AS NVARCHAR(MAX)))), N'') IS NOT NULL
          AND LTRIM(RTRIM(j.[value])) <> N''
          AND LOWER(LTRIM(RTRIM(j.[value]))) NOT IN (N'1', N'2', N'3', N'4', N'minimal', N'low', N'medium', N'high', N'xhigh', N'max')
    )
    BEGIN
        THROW 51221, N'[Step 2] dbo.ModelSnapshot.SupportedEfforts 存在无法识别的 token', 1;
    END

    UPDATE ms
    SET SupportedEfforts = normalized.SupportedEfforts
    FROM dbo.ModelSnapshot ms
    CROSS APPLY
    (
        SELECT CASE
            WHEN NULLIF(LTRIM(RTRIM(CAST(ms.SupportedEfforts AS NVARCHAR(MAX)))), N'') IS NULL THEN NULL
            ELSE
            (
                SELECT STRING_AGG(mapped.Token, ',') WITHIN GROUP (ORDER BY mapped.Ordinal)
                FROM
                (
                    SELECT TRY_CAST(j.[key] AS INT) AS Ordinal,
                           CASE LOWER(LTRIM(RTRIM(j.[value])))
                               WHEN N'1' THEN 'minimal'
                               WHEN N'2' THEN 'low'
                               WHEN N'3' THEN 'medium'
                               WHEN N'4' THEN 'high'
                               ELSE LOWER(LTRIM(RTRIM(j.[value])))
                           END AS Token
                    FROM OPENJSON(
                        CONCAT(
                            '["',
                            REPLACE(REPLACE(REPLACE(CAST(ms.SupportedEfforts AS NVARCHAR(MAX)), N'\', N'\\'), N'"', N'\"'), N',', N'","'),
                            '"]'
                        )
                    ) j
                    WHERE LTRIM(RTRIM(j.[value])) <> N''
                ) mapped
                WHERE mapped.Token IS NOT NULL
            )
        END AS SupportedEfforts
    ) normalized
    WHERE ISNULL(CAST(ms.SupportedEfforts AS NVARCHAR(MAX)), N'') <> ISNULL(CAST(normalized.SupportedEfforts AS NVARCHAR(MAX)), N'');

    ALTER TABLE dbo.ModelSnapshot
    ALTER COLUMN SupportedEfforts VARCHAR(200) NULL;

    UPDATE dbo.ModelSnapshot
    SET SupportedEfforts = NULL
    WHERE LTRIM(RTRIM(ISNULL(SupportedEfforts, ''))) = '';

    PRINT N'    -> 已完成 dbo.ModelSnapshot.SupportedEfforts 规范化';
END

IF COL_LENGTH(N'dbo.ChatConfig', N'Effort') IS NOT NULL
BEGIN
    DECLARE @ChatConfigEffortDefault SYSNAME;
    SELECT @ChatConfigEffortDefault = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.ChatConfig')
      AND c.name = N'Effort';

    IF @ChatConfigEffortDefault IS NOT NULL
    BEGIN
        DECLARE @DropChatConfigEffortDefaultSql NVARCHAR(MAX);
        SET @DropChatConfigEffortDefaultSql = N'ALTER TABLE dbo.ChatConfig DROP CONSTRAINT ' + QUOTENAME(@ChatConfigEffortDefault) + N';';
        EXEC(@DropChatConfigEffortDefaultSql);
    END

    ALTER TABLE dbo.ChatConfig
    ALTER COLUMN Effort VARCHAR(50) NULL;

    IF EXISTS (
        SELECT 1
        FROM dbo.ChatConfig
        WHERE NULLIF(LTRIM(RTRIM(Effort)), '') IS NOT NULL
          AND LOWER(LTRIM(RTRIM(Effort))) NOT IN ('0', '1', '2', '3', '4', 'minimal', 'low', 'medium', 'high', 'xhigh', 'max')
    )
    BEGIN
        THROW 51222, N'[Step 2] dbo.ChatConfig.Effort 存在无法识别的值', 1;
    END

    UPDATE dbo.ChatConfig
    SET Effort = CASE LOWER(LTRIM(RTRIM(Effort)))
        WHEN '0' THEN NULL
        WHEN '1' THEN 'minimal'
        WHEN '2' THEN 'low'
        WHEN '3' THEN 'medium'
        WHEN '4' THEN 'high'
        WHEN '' THEN NULL
        ELSE LOWER(LTRIM(RTRIM(Effort)))
    END;

    PRINT N'    -> 已完成 dbo.ChatConfig.Effort 规范化';
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Effort') IS NOT NULL
BEGIN
    DECLARE @ChatConfigSnapshotEffortDefault SYSNAME;
    SELECT @ChatConfigSnapshotEffortDefault = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.ChatConfigSnapshot')
      AND c.name = N'Effort';

    IF @ChatConfigSnapshotEffortDefault IS NOT NULL
    BEGIN
        DECLARE @DropChatConfigSnapshotEffortDefaultSql NVARCHAR(MAX);
        SET @DropChatConfigSnapshotEffortDefaultSql = N'ALTER TABLE dbo.ChatConfigSnapshot DROP CONSTRAINT ' + QUOTENAME(@ChatConfigSnapshotEffortDefault) + N';';
        EXEC(@DropChatConfigSnapshotEffortDefaultSql);
    END

    ALTER TABLE dbo.ChatConfigSnapshot
    ALTER COLUMN Effort VARCHAR(50) NULL;

    IF EXISTS (
        SELECT 1
        FROM dbo.ChatConfigSnapshot
        WHERE NULLIF(LTRIM(RTRIM(Effort)), '') IS NOT NULL
          AND LOWER(LTRIM(RTRIM(Effort))) NOT IN ('0', '1', '2', '3', '4', 'minimal', 'low', 'medium', 'high', 'xhigh', 'max')
    )
    BEGIN
        THROW 51224, N'[Step 2] dbo.ChatConfigSnapshot.Effort 存在无法识别的值', 1;
    END

    UPDATE dbo.ChatConfigSnapshot
    SET Effort = CASE LOWER(LTRIM(RTRIM(Effort)))
        WHEN '0' THEN NULL
        WHEN '1' THEN 'minimal'
        WHEN '2' THEN 'low'
        WHEN '3' THEN 'medium'
        WHEN '4' THEN 'high'
        WHEN '' THEN NULL
        ELSE LOWER(LTRIM(RTRIM(Effort)))
    END;

    PRINT N'    -> 已完成 dbo.ChatConfigSnapshot.Effort 规范化';
END

IF COL_LENGTH(N'dbo.ChatConfig', N'Compression') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.ChatConfig
        WHERE Compression IS NOT NULL
          AND (Compression < 0 OR Compression > 100)
    )
    BEGIN
        THROW 51223, N'[Step 2] dbo.ChatConfig.Compression 超出 0-100 范围', 1;
    END

    ALTER TABLE dbo.ChatConfig
    ALTER COLUMN Compression TINYINT NULL;

    PRINT N'    -> 已将 dbo.ChatConfig.Compression 调整为 TINYINT';
END

IF COL_LENGTH(N'dbo.ChatConfigSnapshot', N'Compression') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.ChatConfigSnapshot
        WHERE Compression IS NOT NULL
          AND (Compression < 0 OR Compression > 100)
    )
    BEGIN
        THROW 51225, N'[Step 2] dbo.ChatConfigSnapshot.Compression 超出 0-100 范围', 1;
    END

    ALTER TABLE dbo.ChatConfigSnapshot
    ALTER COLUMN Compression TINYINT NULL;

    PRINT N'    -> 已将 dbo.ChatConfigSnapshot.Compression 调整为 TINYINT';
END

UPDATE dbo.ChatConfigSnapshot
SET HashCode = NULL
WHERE HashCode IS NOT NULL;

PRINT N'    -> 已将 dbo.ChatConfigSnapshot.HashCode 置空以等待新语义重建';

GO

PRINT N'[1.12.0] 1.12 数据迁移完成';

GO