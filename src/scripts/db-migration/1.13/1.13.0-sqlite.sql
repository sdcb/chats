-- [1.13.0] MCP ServerInstructions / UserMcp.ShowShortcut migration for SQLite

BEGIN TRANSACTION;

-- MCP global instructions, mapped from SDK McpClient.ServerInstructions.
ALTER TABLE "McpServer"
ADD COLUMN "ServerInstructions" TEXT NULL;

-- User-level flag for showing an MCP shortcut button in ChatInput.
-- SQLite keeps the DEFAULT in schema; it also backfills existing rows to 0.
ALTER TABLE "UserMcp"
ADD COLUMN "ShowShortcut" INTEGER NOT NULL DEFAULT 0;

COMMIT;
