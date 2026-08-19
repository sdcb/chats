-- [1.15.0] MCP names and tool metadata migration for SQLite
-- Mirrors src/scripts/db-migration/1.15/1.15.0.sql (SQL Server).
-- Not idempotent: run exactly once against a 1.14-era database.
--
-- SQLite cannot ALTER a column's nullability or remove a temporary DEFAULT,
-- so McpServer and McpTool are rebuilt. Foreign-key enforcement must be
-- disabled before the transaction while their parent/child tables are swapped.

PRAGMA foreign_keys = OFF;

BEGIN IMMEDIATE TRANSACTION;

-- =============================================
-- Step 1: McpServer.Name / DisplayName
-- 原 Label 成为可空的用户显示名称；协议名称 Name 仅允许 ASCII
-- 字母、数字、下划线和短横线。存量 Name 直接使用稳定唯一的 Id。
-- =============================================
CREATE TABLE "McpServer_1_15_New" (
    "Id"                 INTEGER NOT NULL
                                 CONSTRAINT "PK_McpServer" PRIMARY KEY AUTOINCREMENT,
    "DisplayName"        TEXT NULL,
    "Url"                TEXT NOT NULL,
    "Headers"            TEXT NULL,
    "CreatedAt"          TEXT NOT NULL,
    "OwnerUserId"        INTEGER NOT NULL,
    "UpdatedAt"          TEXT NOT NULL,
    "ServerInstructions" TEXT NULL,
    "Name"               TEXT NOT NULL,

    CONSTRAINT "FK_McpServer_User_OwnerUserId"
        FOREIGN KEY ("OwnerUserId") REFERENCES "User" ("Id"),
    CONSTRAINT "CK_McpServer_Name_SafeCharacters"
        CHECK (
            length("Name") BETWEEN 1 AND 50
            AND "Name" NOT GLOB '*[^A-Za-z0-9_-]*'
        )
);

INSERT INTO "McpServer_1_15_New" (
    "Id",
    "DisplayName",
    "Url",
    "Headers",
    "CreatedAt",
    "OwnerUserId",
    "UpdatedAt",
    "ServerInstructions",
    "Name"
)
SELECT
    "Id",
    "Label",
    "Url",
    "Headers",
    "CreatedAt",
    "OwnerUserId",
    "UpdatedAt",
    "ServerInstructions",
    CAST("Id" AS TEXT)
FROM "McpServer";

DROP TABLE "McpServer";
ALTER TABLE "McpServer_1_15_New" RENAME TO "McpServer";

CREATE UNIQUE INDEX "UX_McpServer_Owner_Name"
ON "McpServer"("OwnerUserId", "Name");

-- =============================================
-- Step 2: McpTool.Title and behavior annotations
-- 存量四个布尔配置统一回填为 false。重建后不保留 DEFAULT，
-- 后续新增工具必须由应用层显式写入这些值。
-- =============================================
CREATE TABLE "McpTool_1_15_New" (
    "Id"          INTEGER NOT NULL
                          CONSTRAINT "PK_McpTool" PRIMARY KEY AUTOINCREMENT,
    "McpServerId" INTEGER NOT NULL,
    "ToolName"    TEXT NOT NULL,
    "Description" TEXT NULL,
    "Parameters"  TEXT NULL,
    "Title"       TEXT NULL,
    "Destructive" INTEGER NOT NULL,
    "Idempotent"  INTEGER NOT NULL,
    "OpenWorld"   INTEGER NOT NULL,
    "ReadOnly"    INTEGER NOT NULL,

    CONSTRAINT "FK_McpTool_McpServer_McpServerId"
        FOREIGN KEY ("McpServerId") REFERENCES "McpServer" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_McpTool_Destructive_Boolean"
        CHECK ("Destructive" IN (0, 1)),
    CONSTRAINT "CK_McpTool_Idempotent_Boolean"
        CHECK ("Idempotent" IN (0, 1)),
    CONSTRAINT "CK_McpTool_OpenWorld_Boolean"
        CHECK ("OpenWorld" IN (0, 1)),
    CONSTRAINT "CK_McpTool_ReadOnly_Boolean"
        CHECK ("ReadOnly" IN (0, 1))
);

INSERT INTO "McpTool_1_15_New" (
    "Id",
    "McpServerId",
    "ToolName",
    "Description",
    "Parameters",
    "Title",
    "Destructive",
    "Idempotent",
    "OpenWorld",
    "ReadOnly"
)
SELECT
    "Id",
    "McpServerId",
    "ToolName",
    "Description",
    "Parameters",
    NULL,
    0,
    0,
    0,
    0
FROM "McpTool";

DROP TABLE "McpTool";
ALTER TABLE "McpTool_1_15_New" RENAME TO "McpTool";

CREATE UNIQUE INDEX "UX_McpTool_Server_Name"
ON "McpTool"("McpServerId", "ToolName");

-- =============================================
-- Step 3: StepContentToolCall.DisplayName
-- 历史工具调用保持 NULL，由应用层回退显示 Name。
-- =============================================
ALTER TABLE "StepContentToolCall"
ADD COLUMN "DisplayName" TEXT NULL;

COMMIT;

PRAGMA foreign_keys = ON;
PRAGMA foreign_key_check;
