PRINT N'[1.16.0] 开始执行模型物理删除兼容迁移';
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- ============================================================
-- Step 1.1: ChatConfig.ModelId 改为可空
--
-- 删除活动 Model 时保留 ChatConfig、ChatSpan 和 ChatPresetSpan，
-- 仅将 ChatConfig.ModelId 置为 NULL。历史快照不参与本次清理。
-- ============================================================
PRINT N'[Step 1.1] 修改 dbo.ChatConfig.ModelId 为可空';

IF OBJECT_ID(N'dbo.ChatConfig', N'U') IS NULL
BEGIN
    THROW 51600, N'[Step 1.1] dbo.ChatConfig 不存在，无法迁移', 1;
END

IF COL_LENGTH(N'dbo.ChatConfig', N'ModelId') IS NULL
BEGIN
    THROW 51601, N'[Step 1.1] dbo.ChatConfig.ModelId 不存在，无法迁移', 1;
END

IF EXISTS
(
    SELECT 1
    FROM dbo.ChatConfig c
    LEFT JOIN dbo.Model m ON m.Id = c.ModelId
    WHERE c.ModelId IS NOT NULL
      AND m.Id IS NULL
)
BEGIN
    THROW 51602, N'[Step 1.1] 存在无法解析的 ChatConfig.ModelId，已停止迁移', 1;
END

IF OBJECT_ID(N'dbo.FK_ChatConfig_Model', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ChatConfig DROP CONSTRAINT FK_ChatConfig_Model;
    PRINT N'    -> 已删除旧外键 FK_ChatConfig_Model';
END

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ChatConfig')
      AND name = N'ModelId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.ChatConfig ALTER COLUMN ModelId SMALLINT NULL;
    PRINT N'    -> 已将 dbo.ChatConfig.ModelId 改为可空';
END
ELSE
BEGIN
    PRINT N'    -> dbo.ChatConfig.ModelId 已为可空，跳过';
END
GO

-- 必须在新的列定义批次中重新创建外键。
PRINT N'[Step 1.2] 为 ChatConfig.ModelId 创建 ON DELETE SET NULL 外键';

IF OBJECT_ID(N'dbo.FK_ChatConfig_Model', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConfig WITH CHECK
    ADD CONSTRAINT FK_ChatConfig_Model
        FOREIGN KEY (ModelId) REFERENCES dbo.Model(Id)
        ON DELETE SET NULL;

    ALTER TABLE dbo.ChatConfig CHECK CONSTRAINT FK_ChatConfig_Model;
    PRINT N'    -> 已创建 FK_ChatConfig_Model (ON DELETE SET NULL)';
END
ELSE
BEGIN
    PRINT N'    -> FK_ChatConfig_Model 已存在，跳过';
END
GO

-- ============================================================
-- Step 1.3: 活动用户模型关联级联删除
-- ============================================================
PRINT N'[Step 1.3] 设置 UserModel 删除策略';

IF OBJECT_ID(N'dbo.FK_UserModel2_Model', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserModel DROP CONSTRAINT FK_UserModel2_Model;
END

IF OBJECT_ID(N'dbo.UserModel', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserModel WITH CHECK
    ADD CONSTRAINT FK_UserModel2_Model
        FOREIGN KEY (ModelId) REFERENCES dbo.Model(Id)
        ON DELETE CASCADE;

    ALTER TABLE dbo.UserModel CHECK CONSTRAINT FK_UserModel2_Model;
    PRINT N'    -> UserModel.ModelId 已设置为 ON DELETE CASCADE';
END
GO

-- ============================================================
-- Step 1.4: API 模型关联级联删除
-- UserApiModel 为 UserApiKey 与 Model 的活动关联表。
-- ============================================================
PRINT N'[Step 1.4] 设置 UserApiModel 删除策略';

IF OBJECT_ID(N'dbo.FK_ApiKeyModel2_Model', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserApiModel DROP CONSTRAINT FK_ApiKeyModel2_Model;
END

IF OBJECT_ID(N'dbo.UserApiModel', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserApiModel WITH CHECK
    ADD CONSTRAINT FK_ApiKeyModel2_Model
        FOREIGN KEY (ModelId) REFERENCES dbo.Model(Id)
        ON DELETE CASCADE;

    ALTER TABLE dbo.UserApiModel CHECK CONSTRAINT FK_ApiKeyModel2_Model;
    PRINT N'    -> UserApiModel.ModelId 已设置为 ON DELETE CASCADE';
END
GO

-- ============================================================
-- Step 1.5: API 缓存级联删除
-- 缓存属于活动数据；历史 usage 不通过 ModelId 关联，不做删除。
-- ============================================================
PRINT N'[Step 1.5] 设置 UserApiCache 删除策略';

IF OBJECT_ID(N'dbo.FK_UserApiCache_ModelId', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserApiCache DROP CONSTRAINT FK_UserApiCache_ModelId;
END

IF OBJECT_ID(N'dbo.UserApiCache', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.UserApiCache WITH CHECK
    ADD CONSTRAINT FK_UserApiCache_ModelId
        FOREIGN KEY (ModelId) REFERENCES dbo.Model(Id)
        ON DELETE CASCADE;

    ALTER TABLE dbo.UserApiCache CHECK CONSTRAINT FK_UserApiCache_ModelId;
    PRINT N'    -> UserApiCache.ModelId 已设置为 ON DELETE CASCADE';
END
GO

-- ============================================================
-- Step 1.6: 迁移后 best-effort 校验
-- ============================================================
PRINT N'[Step 1.6] 执行迁移后校验';

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ChatConfig')
      AND name = N'ModelId'
      AND is_nullable = 0
)
BEGIN
    THROW 51610, N'[Step 1.6] ChatConfig.ModelId 仍为非空列', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ChatConfig_Model'
      AND parent_object_id = OBJECT_ID(N'dbo.ChatConfig')
)
BEGIN
    THROW 51611, N'[Step 1.6] FK_ChatConfig_Model 不存在', 1;
END

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + N'.' + OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys fk
WHERE fk.name IN
(
    N'FK_ChatConfig_Model',
    N'FK_UserModel2_Model',
    N'FK_ApiKeyModel2_Model',
    N'FK_UserApiCache_ModelId'
)
ORDER BY fk.name;

PRINT N'[1.16.0] 模型物理删除兼容迁移完成';
GO
