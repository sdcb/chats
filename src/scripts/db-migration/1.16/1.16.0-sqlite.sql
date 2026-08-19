-- [1.16.0] Model physical deletion compatibility migration for SQLite
-- Mirrors src/scripts/db-migration/1.16/1.16.0.sql (SQL Server).
-- Not idempotent: run exactly once against a 1.15-era database.
--
-- SQLite cannot ALTER a column's nullability or an existing foreign-key
-- action, so the four affected tables are rebuilt. Foreign-key enforcement
-- must be disabled before the transaction while the tables are swapped.

PRAGMA foreign_keys = OFF;

BEGIN IMMEDIATE TRANSACTION;

-- ============================================================
-- Step 1.1: ChatConfig.ModelId 改为可空，并使用 ON DELETE SET NULL
--
-- 删除活动 Model 时保留 ChatConfig、ChatSpan 和 ChatPresetSpan，
-- 仅将 ChatConfig.ModelId 置为 NULL。历史快照不参与本次清理。
-- ============================================================
CREATE TABLE "ChatConfig_1_16_New" (
    "Id"                   INTEGER NOT NULL
                                   CONSTRAINT "PK_ChatConfig" PRIMARY KEY AUTOINCREMENT,
    "ModelId"              INTEGER NULL,
    "SystemPrompt"          TEXT NULL,
    "Temperature"           REAL NULL,
    "WebSearchEnabled"      INTEGER NOT NULL,
    "MaxOutputTokens"       INTEGER NULL,
    "Effort"                TEXT NULL,
    "CodeExecutionEnabled"  INTEGER NOT NULL,
    "ImageSize"             TEXT NULL,
    "ThinkingBudget"        INTEGER NULL,
    "Format"                TEXT NULL,
    "Compression"           INTEGER NULL,

    CONSTRAINT "FK_ChatConfig_Model"
        FOREIGN KEY ("ModelId") REFERENCES "Model" ("Id") ON DELETE SET NULL
);

INSERT INTO "ChatConfig_1_16_New" (
    "Id",
    "ModelId",
    "SystemPrompt",
    "Temperature",
    "WebSearchEnabled",
    "MaxOutputTokens",
    "Effort",
    "CodeExecutionEnabled",
    "ImageSize",
    "ThinkingBudget",
    "Format",
    "Compression"
)
SELECT
    "Id",
    "ModelId",
    "SystemPrompt",
    "Temperature",
    "WebSearchEnabled",
    "MaxOutputTokens",
    "Effort",
    "CodeExecutionEnabled",
    "ImageSize",
    "ThinkingBudget",
    "Format",
    "Compression"
FROM "ChatConfig";

DROP TABLE "ChatConfig";
ALTER TABLE "ChatConfig_1_16_New" RENAME TO "ChatConfig";

CREATE INDEX "IX_ChatConfig_ModelId"
ON "ChatConfig" ("ModelId");

-- ============================================================
-- Step 1.2: UserModel.ModelId 使用 ON DELETE CASCADE
-- ============================================================
CREATE TABLE "UserModel_1_16_New" (
    "Id"            INTEGER NOT NULL
                            CONSTRAINT "PK_UserModel2" PRIMARY KEY AUTOINCREMENT,
    "ModelId"       INTEGER NOT NULL,
    "ExpiresAt"     TEXT NOT NULL,
    "TokenBalance"  INTEGER NOT NULL,
    "CountBalance"  INTEGER NOT NULL,
    "CreatedAt"     TEXT NOT NULL,
    "UpdatedAt"     TEXT NOT NULL,
    "UserId"        INTEGER NOT NULL,

    CONSTRAINT "FK_UserModel2_Model"
        FOREIGN KEY ("ModelId") REFERENCES "Model" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserModel_UserId"
        FOREIGN KEY ("UserId") REFERENCES "User" ("Id")
);

INSERT INTO "UserModel_1_16_New" (
    "Id",
    "ModelId",
    "ExpiresAt",
    "TokenBalance",
    "CountBalance",
    "CreatedAt",
    "UpdatedAt",
    "UserId"
)
SELECT
    "Id",
    "ModelId",
    "ExpiresAt",
    "TokenBalance",
    "CountBalance",
    "CreatedAt",
    "UpdatedAt",
    "UserId"
FROM "UserModel";

DROP TABLE "UserModel";
ALTER TABLE "UserModel_1_16_New" RENAME TO "UserModel";

CREATE INDEX "IX_UserModel_UserId"
ON "UserModel" ("UserId");

CREATE INDEX "IX_UserModel2_ModelId"
ON "UserModel" ("ModelId");

-- ============================================================
-- Step 1.3: UserApiModel.ModelId 使用 ON DELETE CASCADE
-- UserApiModel 为 UserApiKey 与 Model 的活动关联表。
-- ============================================================
CREATE TABLE "UserApiModel_1_16_New" (
    "ApiKeyId"  INTEGER NOT NULL,
    "ModelId"   INTEGER NOT NULL,

    CONSTRAINT "PK_ApiKeyModel2" PRIMARY KEY ("ApiKeyId", "ModelId"),
    CONSTRAINT "FK_ApiKeyModel2_ApiKey"
        FOREIGN KEY ("ApiKeyId") REFERENCES "UserApiKey" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ApiKeyModel2_Model"
        FOREIGN KEY ("ModelId") REFERENCES "Model" ("Id") ON DELETE CASCADE
);

INSERT INTO "UserApiModel_1_16_New" (
    "ApiKeyId",
    "ModelId"
)
SELECT
    "ApiKeyId",
    "ModelId"
FROM "UserApiModel";

DROP TABLE "UserApiModel";
ALTER TABLE "UserApiModel_1_16_New" RENAME TO "UserApiModel";

CREATE INDEX "IX_UserApiModel_ModelId"
ON "UserApiModel" ("ModelId");

-- ============================================================
-- Step 1.4: UserApiCache.ModelId 使用 ON DELETE CASCADE
-- 缓存属于活动数据；历史 usage 不通过 ModelId 关联，不做删除。
-- ============================================================
CREATE TABLE "UserApiCache_1_16_New" (
    "Id"               INTEGER NOT NULL
                               CONSTRAINT "PK_UserApiCache" PRIMARY KEY AUTOINCREMENT,
    "UserApiKeyId"     INTEGER NOT NULL,
    "ModelId"          INTEGER NOT NULL,
    "RequestHashCode"  INTEGER NOT NULL,
    "Expires"          TEXT NOT NULL,
    "ClientInfoId"     INTEGER NOT NULL,
    "CreatedAt"        TEXT NOT NULL,

    CONSTRAINT "FK_UserApiCache_ClientInfoId"
        FOREIGN KEY ("ClientInfoId") REFERENCES "ClientInfo" ("Id"),
    CONSTRAINT "FK_UserApiCache_ModelId"
        FOREIGN KEY ("ModelId") REFERENCES "Model" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserApiCache_UserApiKeyId"
        FOREIGN KEY ("UserApiKeyId") REFERENCES "UserApiKey" ("Id") ON DELETE CASCADE
);

INSERT INTO "UserApiCache_1_16_New" (
    "Id",
    "UserApiKeyId",
    "ModelId",
    "RequestHashCode",
    "Expires",
    "ClientInfoId",
    "CreatedAt"
)
SELECT
    "Id",
    "UserApiKeyId",
    "ModelId",
    "RequestHashCode",
    "Expires",
    "ClientInfoId",
    "CreatedAt"
FROM "UserApiCache";

DROP TABLE "UserApiCache";
ALTER TABLE "UserApiCache_1_16_New" RENAME TO "UserApiCache";

CREATE INDEX "IX_UserApiCache_ClientInfoId"
ON "UserApiCache" ("ClientInfoId");

CREATE INDEX "IX_UserApiCache_CreatedAt"
ON "UserApiCache" ("Expires");

CREATE INDEX "IX_UserApiCache_ModelId"
ON "UserApiCache" ("ModelId");

CREATE INDEX "IX_UserApiCache_RequestHashCode"
ON "UserApiCache" ("RequestHashCode");

CREATE INDEX "IX_UserApiCache_UserApiKeyId"
ON "UserApiCache" ("UserApiKeyId");

COMMIT;

PRAGMA foreign_keys = ON;

-- ============================================================
-- Step 1.5: 迁移后 best-effort 校验
--
-- foreign_key_check 无输出表示不存在外键违规。第二个结果集应显示：
-- ChatConfig / SET NULL，其余三张表 / CASCADE。
-- ============================================================
PRAGMA foreign_key_check;

SELECT
    'ChatConfig' AS "ParentTable",
    f."from" AS "ForeignKeyColumn",
    f."on_delete" AS "DeleteAction"
FROM pragma_foreign_key_list('ChatConfig') AS f
WHERE f."table" = 'Model'

UNION ALL

SELECT
    'UserModel',
    f."from",
    f."on_delete"
FROM pragma_foreign_key_list('UserModel') AS f
WHERE f."table" = 'Model'

UNION ALL

SELECT
    'UserApiModel',
    f."from",
    f."on_delete"
FROM pragma_foreign_key_list('UserApiModel') AS f
WHERE f."table" = 'Model'

UNION ALL

SELECT
    'UserApiCache',
    f."from",
    f."on_delete"
FROM pragma_foreign_key_list('UserApiCache') AS f
WHERE f."table" = 'Model';
