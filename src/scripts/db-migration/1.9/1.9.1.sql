PRINT N'[1.9.1] 开始执行数据库迁移任务';

-- =============================================
-- 第一步：Model 表价格字段改名与新增缓存价格
-- =============================================
PRINT N'[Step 1.1] 将 Model.InputTokenPrice1M 重命名为 InputFreshTokenPrice1M';

IF COL_LENGTH(N'dbo.[Model]', N'InputFreshTokenPrice1M') IS NULL
   AND COL_LENGTH(N'dbo.[Model]', N'InputTokenPrice1M') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.[Model].InputTokenPrice1M', N'InputFreshTokenPrice1M', N'COLUMN';
    PRINT N'    -> 已重命名 Model.InputTokenPrice1M -> InputFreshTokenPrice1M';
END
ELSE
BEGIN
    PRINT N'    -> 无需重命名或列不存在';
END

GO

PRINT N'[Step 1.2] 新增 Model.InputCachedTokenPrice1M 列';

IF COL_LENGTH(N'dbo.[Model]', N'InputCachedTokenPrice1M') IS NULL
BEGIN
    ALTER TABLE dbo.[Model]
    ADD InputCachedTokenPrice1M DECIMAL(9, 5) NOT NULL
        CONSTRAINT DF_Model_InputCachedTokenPrice1M DEFAULT(0);

    EXEC(N'UPDATE dbo.[Model] SET InputCachedTokenPrice1M = InputFreshTokenPrice1M;');

    ALTER TABLE dbo.[Model]
    DROP CONSTRAINT DF_Model_InputCachedTokenPrice1M;

    PRINT N'    -> 已新增并初始化 Model.InputCachedTokenPrice1M 列';
END
ELSE
BEGIN
    PRINT N'    -> Model.InputCachedTokenPrice1M 已存在，跳过';
END

GO

-- =============================================
-- 第二步：UserModelUsage 输入用量/费用字段重命名与新增
-- =============================================
PRINT N'[Step 2.1] 将 UserModelUsage.InputTokens 重命名为 InputFreshTokens';

IF COL_LENGTH(N'dbo.UserModelUsage', N'InputFreshTokens') IS NULL
   AND COL_LENGTH(N'dbo.UserModelUsage', N'InputTokens') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.UserModelUsage.InputTokens', N'InputFreshTokens', N'COLUMN';
    PRINT N'    -> 已重命名 UserModelUsage.InputTokens -> InputFreshTokens';
END
ELSE
BEGIN
    PRINT N'    -> 无需重命名或列不存在';
END

GO

PRINT N'[Step 2.2] 将 UserModelUsage.InputCost 重命名为 InputFreshCost';

IF COL_LENGTH(N'dbo.UserModelUsage', N'InputFreshCost') IS NULL
   AND COL_LENGTH(N'dbo.UserModelUsage', N'InputCost') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.UserModelUsage.InputCost', N'InputFreshCost', N'COLUMN';
    PRINT N'    -> 已重命名 UserModelUsage.InputCost -> InputFreshCost';
END
ELSE
BEGIN
    PRINT N'    -> 无需重命名或列不存在';
END

GO

PRINT N'[Step 2.3] 处理 UserModelUsage.InputCachedTokens 列';

IF COL_LENGTH(N'dbo.UserModelUsage', N'InputCachedTokens') IS NULL
BEGIN
    IF COL_LENGTH(N'dbo.UserModelUsage', N'CacheTokens') IS NOT NULL
    BEGIN
        EXEC sp_rename N'dbo.UserModelUsage.CacheTokens', N'InputCachedTokens', N'COLUMN';
        PRINT N'    -> 已重命名 UserModelUsage.CacheTokens -> InputCachedTokens';
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.UserModelUsage
        ADD InputCachedTokens INT NOT NULL CONSTRAINT DF_UserModelUsage_InputCachedTokens DEFAULT(0);

        ALTER TABLE dbo.UserModelUsage
        DROP CONSTRAINT DF_UserModelUsage_InputCachedTokens;

        PRINT N'    -> 已新增 UserModelUsage.InputCachedTokens 列';
    END
END
ELSE
BEGIN
    PRINT N'    -> UserModelUsage.InputCachedTokens 已存在，跳过';
END

GO

PRINT N'[Step 2.4] 处理 UserModelUsage.InputCachedCost 列';

IF COL_LENGTH(N'dbo.UserModelUsage', N'InputCachedCost') IS NULL
BEGIN
    IF COL_LENGTH(N'dbo.UserModelUsage', N'CacheCost') IS NOT NULL
    BEGIN
        EXEC sp_rename N'dbo.UserModelUsage.CacheCost', N'InputCachedCost', N'COLUMN';
        PRINT N'    -> 已重命名 UserModelUsage.CacheCost -> InputCachedCost';
    END
    ELSE
    BEGIN
        ALTER TABLE dbo.UserModelUsage
        ADD InputCachedCost DECIMAL(14, 8) NOT NULL CONSTRAINT DF_UserModelUsage_InputCachedCost DEFAULT(0);

        ALTER TABLE dbo.UserModelUsage
        DROP CONSTRAINT DF_UserModelUsage_InputCachedCost;

        PRINT N'    -> 已新增 UserModelUsage.InputCachedCost 列';
    END
END
ELSE
BEGIN
    PRINT N'    -> UserModelUsage.InputCachedCost 已存在，跳过';
END

GO

-- =============================================
-- 第三步：StepContentThink.Signature 从 varbinary 转换为 nvarchar(max)
-- =============================================
PRINT N'[Step 3.1] 新增 StepContentThink.SignatureBase64 临时列';

IF COL_LENGTH(N'dbo.StepContentThink', N'SignatureBase64') IS NULL
BEGIN
    ALTER TABLE dbo.StepContentThink
    ADD SignatureBase64 NVARCHAR(MAX) NULL;

    PRINT N'    -> 已新增 StepContentThink.SignatureBase64 列';
END
ELSE
BEGIN
    PRINT N'    -> StepContentThink.SignatureBase64 已存在，跳过';
END

GO

PRINT N'[Step 3.2] 将 Signature (varbinary) 数据转换为 Base64 并迁移到 SignatureBase64';

UPDATE dbo.StepContentThink
SET SignatureBase64 = CAST(N'' AS XML).value('xs:base64Binary(xs:hexBinary(sql:column("Signature")))', 'NVARCHAR(MAX)')
WHERE Signature IS NOT NULL AND SignatureBase64 IS NULL;

PRINT N'    -> 已完成数据迁移，共更新 ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' 行';

GO

PRINT N'[Step 3.3] 删除原 Signature 列';

IF COL_LENGTH(N'dbo.StepContentThink', N'Signature') IS NOT NULL
BEGIN
    ALTER TABLE dbo.StepContentThink
    DROP COLUMN Signature;

    PRINT N'    -> 已删除原 Signature 列';
END
ELSE
BEGIN
    PRINT N'    -> 原 Signature 列不存在，跳过';
END

GO

PRINT N'[Step 3.4] 将 SignatureBase64 重命名为 Signature';

IF COL_LENGTH(N'dbo.StepContentThink', N'Signature') IS NULL
   AND COL_LENGTH(N'dbo.StepContentThink', N'SignatureBase64') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.StepContentThink.SignatureBase64', N'Signature', N'COLUMN';
    PRINT N'    -> 已重命名 SignatureBase64 -> Signature';
END
ELSE
BEGIN
    PRINT N'    -> 无需重命名或列状态异常';
END

GO

PRINT N'[1.9.1] 所有迁移步骤已完成';
GO
