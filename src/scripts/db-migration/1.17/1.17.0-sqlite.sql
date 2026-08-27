-- [1.17.0] Image generation background configuration migration for SQLite
-- Mirrors src/scripts/db-migration/1.17/1.17.0.sql (SQL Server).
-- Not idempotent: run exactly once against a 1.16-era database.
--
-- SQLite supports adding these nullable columns directly, so no table rebuild
-- is required and existing rows remain NULL (request field omitted).

PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE TRANSACTION;

-- ============================================================
-- Step 1: ChatConfig.Background
-- ============================================================
ALTER TABLE "ChatConfig"
ADD COLUMN "Background" TEXT NULL
    CHECK ("Background" IS NULL OR "Background" IN ('transparent', 'opaque', 'auto'));

-- ============================================================
-- Step 2: ChatConfigSnapshot.Background
-- ============================================================
ALTER TABLE "ChatConfigSnapshot"
ADD COLUMN "Background" TEXT NULL
    CHECK ("Background" IS NULL OR "Background" IN ('transparent', 'opaque', 'auto'));

COMMIT;

-- ============================================================
-- Step 3: best-effort verification
-- ============================================================
SELECT
    m.name AS "TableName",
    p.name AS "ColumnName",
    p.type AS "DataType",
    p."notnull" AS "NotNull"
FROM sqlite_master AS m
JOIN pragma_table_info(m.name) AS p
  ON m.type = 'table'
WHERE m.name IN ('ChatConfig', 'ChatConfigSnapshot')
  AND p.name = 'Background'
ORDER BY m.name;

PRAGMA foreign_key_check;
