-- =============================================
-- 1.8.1 数据库迁移脚本
-- 清理 Azure AI Foundry (Provider=1) ModelKey Host 中的 api-version 参数
-- =============================================

PRINT N'[1.8.1] 开始清理 Azure AI Foundry ModelKey Host 中的 api-version 参数';

-- 清理以 /openai/v1?api-version=preview 结尾的 Host
UPDATE dbo.ModelKey
SET Host = LEFT(Host, CHARINDEX('?api-version=', Host) - 1)
WHERE ModelProviderId = 1  -- Azure AI Foundry
  AND Host IS NOT NULL
  AND Host LIKE '%/openai/v1?api-version=%';

DECLARE @UpdatedCount INT = @@ROWCOUNT;

PRINT N'[1.8.1] 已更新 ' + CAST(@UpdatedCount AS NVARCHAR(10)) + N' 条 ModelKey 记录';

-- 显示更新后的记录（用于验证）
IF @UpdatedCount > 0
BEGIN
    PRINT N'[1.8.1] 受影响的 ModelKey:';
    SELECT 
        Id,
        Name,
        Host,
        CreatedAt
    FROM dbo.ModelKey
    WHERE ModelProviderId = 1
      AND Host IS NOT NULL
    ORDER BY Id;
END

PRINT N'[1.8.1] 迁移完成';
GO
