PRINT N'[1.10.1] 开始执行数据库迁移任务';

-- =============================================
-- 扩展 ClientUserAgent.UserAgent 字段长度
-- =============================================
PRINT N'[Step 1] 扩展 ClientUserAgent.UserAgent 字段长度到 500';

-- 检查列长度是否不为 500
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.ClientUserAgent')
      AND c.name = N'UserAgent'
      AND c.max_length <> 500
)
BEGIN
    ALTER TABLE dbo.ClientUserAgent 
    ALTER COLUMN UserAgent VARCHAR(500) NOT NULL;
    
    PRINT N'    -> 已将 UserAgent 列长度扩展到 500';
END
ELSE
BEGIN
    PRINT N'    -> UserAgent 列长度已经是 500，跳过';
END

GO

PRINT N'[1.10.1] 数据库迁移任务完成';
