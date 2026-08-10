-- [1.14.0] Chats 1.14.0 database migration for SQLite
-- Mirrors src/scripts/db-migration/1.14/1.14.0.sql (SQL Server).
-- Not idempotent: run exactly once against a 1.13-era database.

BEGIN TRANSACTION;

-- =============================================
-- Step 1: StepContentText.ContextTemplate
-- NULL 表示模型上下文直接使用 Content；非 NULL 时由应用层
-- 使用 Content 替换模板中的内容占位符，生成模型实际接收的文本。
-- 历史数据无需回填，保持 NULL 即可维持原有行为。
-- =============================================
ALTER TABLE "StepContentText"
ADD COLUMN "ContextTemplate" TEXT NULL;

-- =============================================
-- Step 2: ChatPreset.IsSystem
-- 系统预设模型组由管理员维护，所有用户可见；现有预设均保持为
-- 用户私有预设（默认 0）。SQLite 没有 INCLUDE，因此将覆盖列
-- 并入索引体（等价于 SQL Server 的 INCLUDE 列）。
-- =============================================
ALTER TABLE "ChatPreset"
ADD COLUMN "IsSystem" INTEGER NOT NULL DEFAULT 0;

CREATE INDEX "IX_ChatPreset_IsSystem_Order"
ON "ChatPreset"("IsSystem", [Order], "Id", "UserId", "Name", "UpdatedAt");

-- =============================================
-- Step 3: User.ApiKeyEnabled
-- 管理员可按用户禁止 API Key 功能；现有用户默认保持启用（1）。
-- DEFAULT 仅用于存量数据回填。
-- =============================================
ALTER TABLE "User"
ADD COLUMN "ApiKeyEnabled" INTEGER NOT NULL DEFAULT 1;

-- =============================================
-- Step 4: UserInitialConfig.Mcps
-- 保存新用户初始化时授予的 MCP 权限及其用户级配置。
-- 存量配置不授予任何 MCP，默认 []。
-- 注：SQL Server 版本的 ISJSON/数组 CHECK 约束无法通过 SQLite
-- ALTER 添加，JSON 合法性由应用层保证。
-- =============================================
ALTER TABLE "UserInitialConfig"
ADD COLUMN "Mcps" TEXT NOT NULL DEFAULT '[]';

-- =============================================
-- Step 5: UserInitialConfig.ApiKeyEnabled
-- 控制按该配置创建的新用户是否允许使用 API Key。
-- 存量配置保持启用（默认 1）。
-- =============================================
ALTER TABLE "UserInitialConfig"
ADD COLUMN "ApiKeyEnabled" INTEGER NOT NULL DEFAULT 1;

-- =============================================
-- Step 6: MCP 标签改为同一所有者内唯一
-- 不同用户可以创建相同标签的 MCP；同一所有者仍不能创建重名 MCP。
-- 新复合索引同时覆盖按 OwnerUserId 查询，因此删除原单列索引。
-- =============================================
DROP INDEX IF EXISTS "UX_McpServer_Label";
DROP INDEX IF EXISTS "IX_McpServer_OwnerUserId";
CREATE UNIQUE INDEX "UX_McpServer_Owner_Label"
ON "McpServer"("OwnerUserId", "Label");

COMMIT;
